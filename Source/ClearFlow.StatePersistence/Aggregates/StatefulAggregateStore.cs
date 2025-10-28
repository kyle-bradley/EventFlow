using EventFlow.Aggregates;
using EventFlow.Aggregates.ExecutionResults;
using EventFlow.Configuration;
using EventFlow.Configuration.Cancellation;
using EventFlow.Core;
using EventFlow.Core.RetryStrategies;
using EventFlow.EventStores;
using EventFlow.Exceptions;
using EventFlow.Extensions;
using EventFlow.Snapshots;
using EventFlow.Subscribers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClearFlow.StatePersistence.Aggregates;
public class StatefulAggregateStore : IAggregateStore
{
    private static readonly IReadOnlyCollection<IDomainEvent> EmptyDomainEventCollection = new IDomainEvent[] { };
    private readonly ILogger<StatefulAggregateStore> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAggregateFactory _aggregateFactory;
    private readonly IEventStore _eventStore;
    private readonly ISnapshotStore _snapshotStore;
    private readonly ITransientFaultHandler<IOptimisticConcurrencyRetryStrategy> _transientFaultHandler;
    private readonly ICancellationConfiguration _cancellationConfiguration;
    private readonly IAggregateStoreResilienceStrategy _aggregateStoreResilienceStrategy;
    private readonly IEventFlowConfiguration _eventFlowConfiguration;
    private readonly IStateStore _stateStore;

    public StatefulAggregateStore(ILogger<StatefulAggregateStore> logger, IServiceProvider serviceProvider, IAggregateFactory aggregateFactory, IEventStore eventStore, ISnapshotStore snapshotStore,
        ITransientFaultHandler<IOptimisticConcurrencyRetryStrategy> transientFaultHandler, ICancellationConfiguration cancellationConfiguration, IAggregateStoreResilienceStrategy aggregateStoreResilienceStrategy, IEventFlowConfiguration eventFlowConfiguration, IStateStore stateStore)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _aggregateFactory = aggregateFactory;
        _eventStore = eventStore;
        _snapshotStore = snapshotStore;
        _transientFaultHandler = transientFaultHandler;
        _cancellationConfiguration = cancellationConfiguration;
        _aggregateStoreResilienceStrategy = aggregateStoreResilienceStrategy;
        _eventFlowConfiguration = eventFlowConfiguration;
        _stateStore = stateStore;
    }

    public async Task<TAggregate> LoadAsync<TAggregate, TIdentity>(TIdentity id, CancellationToken cancellationToken)
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
    {
        var aggregate = await _aggregateFactory.CreateNewAggregateAsync<TAggregate, TIdentity>(id).ConfigureAwait(false);
        var inherits = typeof(TAggregate).IsAssignableFrom(typeof(IStatefulAggregateRoot<TIdentity>));
        var inheritsSec = typeof(TAggregate).IsAssignableTo(typeof(IStatefulAggregateRoot<TIdentity>));

        var castAggregate = aggregate as IStatefulAggregateRoot<TIdentity>;
        if (castAggregate == null)
        {
            throw new InvalidOperationException($"Type {typeof(TAggregate).PrettyPrint()} needs to use StatefulAggregateRoot when state extension is used.");
        }

        await castAggregate.LoadAsync(_eventStore, _snapshotStore, _stateStore, cancellationToken).ConfigureAwait(false);

        return (TAggregate)castAggregate;
    }

    public async Task<IReadOnlyCollection<IDomainEvent>> StoreAsync<TAggregate, TIdentity>(TAggregate aggregate, ISourceId sourceId, CancellationToken cancellationToken)
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
    {
        var castAggregate = aggregate as IStatefulAggregateRoot<TIdentity>;
        if (castAggregate == null)
        {
            throw new InvalidOperationException($"Type {typeof(TAggregate).PrettyPrint()} needs to use StatefulAggregateRoot when state extension is used.");
        }

        var domainEvents = await castAggregate.CommitAsync(
            _eventStore,
            _snapshotStore,
            _stateStore,
            sourceId,
            cancellationToken)
            .ConfigureAwait(false);

        if (domainEvents.Any())
        {
            var domainEventPublisher = _serviceProvider.GetRequiredService<IDomainEventPublisher>();
            await domainEventPublisher.PublishAsync(
                domainEvents,
                cancellationToken)
                .ConfigureAwait(false);
        }

        return domainEvents;
    }

    public async Task<IReadOnlyCollection<IDomainEvent>> UpdateAsync<TAggregate, TIdentity>(
        TIdentity id,
        ISourceId sourceId,
        Func<TAggregate, CancellationToken, Task> updateAggregate,
        CancellationToken cancellationToken)
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
    {
        var aggregateUpdateResult = await UpdateAsync<TAggregate, TIdentity, IExecutionResult>(
            id,
            sourceId,
            async (a, c) =>
            {
                await updateAggregate(a, c).ConfigureAwait(false);
                return ExecutionResult.Success();
            },
            cancellationToken)
            .ConfigureAwait(false);

        return aggregateUpdateResult.DomainEvents;
    }

    public async Task<IAggregateUpdateResult<TExecutionResult>> UpdateAsync<TAggregate, TIdentity, TExecutionResult>(
        TIdentity id,
        ISourceId sourceId,
        Func<TAggregate, CancellationToken, Task<TExecutionResult>> updateAggregate,
        CancellationToken cancellationToken)
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
        where TExecutionResult : IExecutionResult
    {
        await _aggregateStoreResilienceStrategy.BeforeAggregateLoad<TAggregate, TIdentity, TExecutionResult>(
                id,
                cancellationToken)
            .ConfigureAwait(false);

        var aggregateUpdateResult = await _transientFaultHandler.TryAsync(
            async c =>
            {
                var aggregate = await LoadAsync<TAggregate, TIdentity>(id, c).ConfigureAwait(false);
                if (aggregate.HasSourceId(sourceId))
                {
                    throw new DuplicateOperationException(
                        sourceId,
                        id,
                        $"Aggregate '{typeof(TAggregate).PrettyPrint()}' has already had operation '{sourceId}' performed");
                }

                cancellationToken = _cancellationConfiguration.Limit(cancellationToken, CancellationBoundary.BeforeUpdatingAggregate);

                await _aggregateStoreResilienceStrategy.BeforeAggregateUpdate<TAggregate, TIdentity, TExecutionResult>(
                        aggregate,
                        updateAggregate,
                        cancellationToken)
                    .ConfigureAwait(false);

                var result = await updateAggregate(aggregate, c).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    _logger.LogDebug(
                        "Execution failed on aggregate {AggregateType}, disregarding any events emitted",
                        typeof(TAggregate).PrettyPrint());
                    return new AggregateUpdateResult<TExecutionResult>(
                        result,
                        EmptyDomainEventCollection);
                }

                cancellationToken = _cancellationConfiguration.Limit(cancellationToken, CancellationBoundary.BeforeCommittingEvents);

                await _aggregateStoreResilienceStrategy.BeforeCommitAsync<TAggregate, TIdentity, TExecutionResult>(
                        aggregate,
                        result,
                        cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    var castAggregate = aggregate as IStatefulAggregateRoot<TIdentity>;
                    if (castAggregate == null)
                    {
                        throw new InvalidOperationException($"Type {typeof(TAggregate).PrettyPrint()} needs to use StatefulAggregateRoot when state extension is used.");
                    }

                    var domainEvents = await castAggregate.CommitAsync(
                        _eventStore,
                        _snapshotStore,
                        _stateStore,
                        sourceId,
                        cancellationToken)
                        .ConfigureAwait(false);

                    await _aggregateStoreResilienceStrategy.CommitSucceededAsync<TAggregate, TIdentity, TExecutionResult>(
                            aggregate,
                            result,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return new AggregateUpdateResult<TExecutionResult>(
                        result,
                        domainEvents);
                }
                catch (OptimisticConcurrencyException) when (!_eventFlowConfiguration.ForwardOptimisticConcurrencyExceptions)
                {
                    throw;
                }
                catch (Exception e)
                {
                    var (handled, updatedAggregateUpdateResult) = await _aggregateStoreResilienceStrategy.HandleCommitFailedAsync<TAggregate, TIdentity, TExecutionResult>(
                            aggregate,
                            result,
                            e,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (!handled)
                    {
                        throw;
                    }

                    return updatedAggregateUpdateResult;
                }
            },
            Label.Named("aggregate-update"),
            cancellationToken)
            .ConfigureAwait(false);

        if (aggregateUpdateResult.Result.IsSuccess &&
            aggregateUpdateResult.DomainEvents.Any())
        {
            await _aggregateStoreResilienceStrategy.BeforeEventPublishAsync<TAggregate, TIdentity, TExecutionResult>(
                    id,
                    aggregateUpdateResult.Result,
                    aggregateUpdateResult.DomainEvents,
                    cancellationToken)
                .ConfigureAwait(false);
            try
            {
                var domainEventPublisher = _serviceProvider.GetRequiredService<IDomainEventPublisher>();
                await domainEventPublisher.PublishAsync(
                        aggregateUpdateResult.DomainEvents,
                        cancellationToken)
                    .ConfigureAwait(false);
                await _aggregateStoreResilienceStrategy.EventPublishSucceededAsync<TAggregate, TIdentity, TExecutionResult>(
                        id,
                        aggregateUpdateResult.Result,
                        aggregateUpdateResult.DomainEvents,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (!await _aggregateStoreResilienceStrategy.HandleEventPublishFailedAsync<TAggregate, TIdentity, TExecutionResult>(
                        id,
                        aggregateUpdateResult.Result,
                        aggregateUpdateResult.DomainEvents,
                        e,
                        cancellationToken)
                    .ConfigureAwait(false))
                {
                    throw;
                }
            }
        }
        else
        {
            await _aggregateStoreResilienceStrategy.EventPublishSkippedAsync<TAggregate, TIdentity, TExecutionResult>(
                    id,
                    aggregateUpdateResult.Result,
                    aggregateUpdateResult.DomainEvents,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return aggregateUpdateResult;
    }


    internal class AggregateUpdateResult<TExecutionResult> : IAggregateUpdateResult<TExecutionResult>
        where TExecutionResult : IExecutionResult
    {
        public TExecutionResult Result { get; }
        public IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

        public AggregateUpdateResult(
            TExecutionResult result,
            IReadOnlyCollection<IDomainEvent> domainEvents)
        {
            Result = result;
            DomainEvents = domainEvents;
        }
    }
}
