using EventFlow.Aggregates;
using EventFlow.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using EventFlow.EventStores;
using ClearFlow.StatePersistence.StateStores;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using EventFlow.Extensions;

namespace ClearFlow.StatePersistence.Aggregates;

public class StateStore : IStateStore
{
    private readonly IDictionary<Type, IStatePersistence> states;
    private readonly IDomainEventFactory eventFactory;
    private readonly ILogger<StateStore> logger;
    private readonly IServiceProvider serviceProvider;
    public ICollection<Type> GetAllStateTypes => states.Keys.ToList();

    public StateStore(IDomainEventFactory eventFactory, IEnumerable<IStatePersistence> states, ILogger<StateStore> logger, IServiceProvider serviceProvider)
    {
        this.states = states.ToDictionary(pair => pair.StateType, pair => pair);
        this.eventFactory = eventFactory;
        this.logger = logger;
        this.serviceProvider = serviceProvider;
    }

    public async Task<AllEventsPage> LoadAllStateEventsAsync(Type stateType, GlobalPosition globalPosition, int pageSize, CancellationToken cancellationToken)
    {
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));

        var persistence = states[stateType];

        var allCommittedEventsPage = await persistence.LoadAllCommittedEvents(
            globalPosition,
            pageSize,
            cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var meta = new Metadata();
            meta.Timestamp = DateTimeOffset.UtcNow;
            var events = allCommittedEventsPage.StateHydratedEvents.Select(aggregateEvent => eventFactory.Create(aggregateEvent, meta, aggregateEvent.IdentityId, 1)).ToList();
            return new AllEventsPage(allCommittedEventsPage.NextGlobalPosition, events);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<TrackedModel<TStateModel>> LoadStateAsync<TAggregate, TIdentity, TStateModel>(TIdentity identity, CancellationToken cancellationToken)
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
        where TStateModel : class, IStateModel
    {
        logger.LogTrace(
            "Fetching state for {AggregateType} with ID {Id}",
            typeof(TAggregate).PrettyPrint(),
            identity);

        var persistence = serviceProvider.GetService<IStatePersistence<TStateModel>>();
        if (persistence == null)
        {
            throw new InvalidOperationException($"No persistence found for {typeof(TStateModel).PrettyPrint()}. Register using IStatePersistence<TStateModel>");
        }

        var stateModel = await persistence.GetOrCreateAsync(
            identity,
            cancellationToken)
            .ConfigureAwait(false);

        if (stateModel == null)
        {
            logger.LogTrace(
                "No state found for {AggregateType} with ID {Id}. Allow for empty models in persistence.",
                typeof(TAggregate).PrettyPrint(),
                identity);

            throw new InvalidOperationException($"No state found for {typeof(TAggregate).PrettyPrint()}. Allow for empty models in persistence.");
        }

        return stateModel;
    }

    public async Task StoreStateAsync<TAggregate, TIdentity, TStateModel>(TIdentity identity, TrackedModel<TStateModel> model, CancellationToken cancellationToken)
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
        where TStateModel : class, IStateModel
    {
        var persistence = serviceProvider.GetService<IStatePersistence<TStateModel>>();
        if (persistence == null)
        {
            throw new InvalidOperationException($"No persistence found for {typeof(TStateModel).PrettyPrint()}. Register using IStatePersistence<TStateModel>");
        }

        await persistence.UpsertAsync(identity, model, cancellationToken).ConfigureAwait(false);
    }
}