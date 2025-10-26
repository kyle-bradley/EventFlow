using ClearFlow.StatePersistence.Aggregates;
using EventFlow.Aggregates;
using EventFlow.Configuration;
using EventFlow.Core.Caching;
using EventFlow.EventStores;
using EventFlow.Extensions;
using EventFlow.ReadStores;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ClearFlow.StatePersistence.ReadStores;

public class StatefulReadModelPopulator : IReadModelPopulator
{
    private readonly ILogger<StatefulReadModelPopulator> _logger;
    private readonly IEventFlowConfiguration _configuration;
    private readonly IStateStore _stateStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadModelPopulatorTracker _readModelPopulatorTracker;
    private readonly IMemoryCache _memoryCache;
    private ConcurrentQueue<AllEventsPage> _pipedEvents = new ConcurrentQueue<AllEventsPage>();
    private static bool eventsLoaded = false;

    public StatefulReadModelPopulator(
        ILogger<StatefulReadModelPopulator> logger,
        IEventFlowConfiguration configuration,
        IStateStore stateStore,
        IServiceProvider serviceProvider,
        IReadModelPopulatorTracker readModelPopulatorTracker,
        IMemoryCache memoryCache)
    {
        _logger = logger;
        _configuration = configuration;
        _stateStore = stateStore;
        _serviceProvider = serviceProvider;
        _readModelPopulatorTracker = readModelPopulatorTracker;
        _memoryCache = memoryCache;
    }

    public Task PurgeAsync<TReadModel>(
        CancellationToken cancellationToken)
        where TReadModel : class, IReadModel
    {
        return PurgeAsync(typeof(TReadModel), cancellationToken);
    }

    public async Task PurgeAsync(
        Type readModelType,
        CancellationToken cancellationToken)
    {
        var readModelStores = ResolveReadModelStores(readModelType);

        var deleteTasks = readModelStores.Select(s => s.DeleteAllAsync(cancellationToken));
        await Task.WhenAll(deleteTasks).ConfigureAwait(false);
    }

    public async Task DeleteAsync(
        string id,
        Type readModelType,
        CancellationToken cancellationToken)
    {
        var readModelStores = ResolveReadModelStores(readModelType);

        _logger.LogTrace(
            "Deleting read model {ReadModelType} with ID {Id}",
            readModelType.PrettyPrint(),
            id);

        var deleteTasks = readModelStores.Select(s => s.DeleteAsync(id, cancellationToken));
        await Task.WhenAll(deleteTasks).ConfigureAwait(false);
    }

    public Task PopulateAsync<TReadModel>(
        CancellationToken cancellationToken)
        where TReadModel : class, IReadModel
    {
        return PopulateAsync(typeof(TReadModel), cancellationToken);
    }

    public Task PopulateAsync(
        Type readModelType,
        CancellationToken cancellationToken)
    {
        return PopulateAsync(new List<Type>() { readModelType }, cancellationToken);
    }

    public async Task PopulateAsync(IReadOnlyCollection<Type> readModelTypes, CancellationToken cancellationToken)
    {
        if (_readModelPopulatorTracker.PopulationInProgress)
        {
            throw new InvalidOperationException("Only one rebuild is able to be active at a time");
        }

        await _readModelPopulatorTracker.PopulationStarted(cancellationToken);

        var combinedReadModelTypeString = string.Join(", ", readModelTypes.Select(type => type.PrettyPrint()));
        _logger.LogInformation("Starting populating of {ReadModelTypes}", combinedReadModelTypeString);

        try
        {
            eventsLoaded = false;

            var stateTypes = _stateStore.GetAllStateTypes;
            var loadEventsTasks = stateTypes.Select(stateType => LoadStateEvents(stateType, cancellationToken));
            var allLoadedTasks = Task.WhenAll(loadEventsTasks);

            var processEventQueueTask = ProcessEventQueue(readModelTypes, cancellationToken);

            await allLoadedTasks;
            eventsLoaded = true;

            await processEventQueueTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Population of readmodels failed");
        }
        finally
        {
            await _readModelPopulatorTracker.PopulationEnded(cancellationToken);
        }

        _logger.LogInformation("Population of readmodels completed");
    }

    private async Task LoadStateEvents(Type stateType, CancellationToken cancellationToken)
    {
        var currentPosition = GlobalPosition.Start;

        while (true)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace(
                    "Loading events starting from {CurrentPosition} and the next {PageSize} for populating",
                    currentPosition,
                    _configuration.LoadReadModelEventPageSize);
            }

            var allEventsPage = await _stateStore.LoadAllStateEventsAsync(stateType,
                currentPosition,
                _configuration.LoadReadModelEventPageSize,
                cancellationToken)
                .ConfigureAwait(false);

            await _readModelPopulatorTracker.NewEventsLoaded(currentPosition, allEventsPage.DomainEvents.Count, cancellationToken);

            currentPosition = allEventsPage.NextGlobalPosition;

            _pipedEvents.Enqueue(allEventsPage);

            if (!allEventsPage.DomainEvents.Any())
            {
                _logger.LogTrace(
                    "No more events in event store with a total of {EventTotal} events",
                    _readModelPopulatorTracker.EventsLoaded);
                break;
            }
        }
    }

    private async Task ProcessEventQueue(IReadOnlyCollection<Type> readModelTypes, CancellationToken cancellationToken)
    {
        var orderedReadModels = readModelTypes.ToLookup(readModel => readModel.GetCustomAttribute<ReadModelOrderAtrribute>()?.ApplyOrder ?? 0, y => y);

        var domainEventsToProcess = new List<IDomainEvent>();
        AllEventsPage fetchedEvents;

        var hasMoreEvents = true;
        do
        {
            var noEventsToReady = !_pipedEvents.Any();
            if (noEventsToReady)
            {
                await Task.Delay(100);
                continue;
            }

            _pipedEvents.TryDequeue(out fetchedEvents);
            if (fetchedEvents == null)
            {
                continue;
            }

            domainEventsToProcess.AddRange(fetchedEvents.DomainEvents);

            hasMoreEvents = fetchedEvents.DomainEvents.Any() || !eventsLoaded;
            var batchExceedsThreshold = domainEventsToProcess.Count >= _configuration.PopulateReadModelEventPageSize;
            var processEvents = !hasMoreEvents || batchExceedsThreshold;
            if (processEvents)
            {
                foreach (var readModelTypeBatch in orderedReadModels.OrderBy(order => order.Key))
                {
                    var orderedReadModelTypes = readModelTypeBatch.ToList();

                    var readModelUpdateTasks = orderedReadModelTypes.Select(readModelType => ProcessEvents(readModelType, domainEventsToProcess, cancellationToken));
                    await Task.WhenAll(readModelUpdateTasks);
                }

                await _readModelPopulatorTracker.NewEventsProcessed(domainEventsToProcess.Count, cancellationToken);
                domainEventsToProcess.Clear();
            }
        }
        while (hasMoreEvents);
    }

    private async Task ProcessEvents(Type readModelType, IReadOnlyCollection<IDomainEvent> processEvents, CancellationToken cancellationToken)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var readStoreManagers = ResolveReadStoreManagers(readModelType);
            long relevantEvents = 0;

            var readModelTypes = new[]
            {
                    typeof( IAmReadModelFor<,,> )
                };

            var aggregateEventTypes = _memoryCache.GetOrCreate(CacheKey.With(GetType(), readModelType.ToString(), nameof(ProcessEvents)),
                e => new HashSet<Type>(readModelType.GetTypeInfo()
                    .GetInterfaces()
                    .Where(i => i.GetTypeInfo().IsGenericType && readModelTypes.Contains(i.GetGenericTypeDefinition()))
                    .Select(i => i.GetTypeInfo().GetGenericArguments()[2])));

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace(
                    "Read model {ReadModelType} is interested in these aggregate events: {AggregateEventTypes}",
                    readModelType.PrettyPrint(),
                    aggregateEventTypes.Select(e => e.PrettyPrint()));
            }

            var domainEvents = processEvents
                .Where(e => aggregateEventTypes.Contains(e.EventType))
                .ToList();
            relevantEvents += domainEvents.Count;

            if (!domainEvents.Any())
            {
                _logger.LogWarning($"Will not populate {readModelType.PrettyPrint()} because no events were found");
                return;
            }

            var applyTasks = readStoreManagers
                .Select(m => m.UpdateReadStoresAsync(domainEvents, cancellationToken));
            await Task.WhenAll(applyTasks).ConfigureAwait(false);

            _logger.LogInformation(
                "Population of read model {ReadModelType} took {Seconds} seconds, in which {RelevantEventCount} was relevant",
                readModelType.PrettyPrint(),
                stopwatch.Elapsed.TotalSeconds,
                relevantEvents);
        }
        catch (Exception e)
        {
            _logger.LogWarning($"Exception when populating: {readModelType}. Details: {e}");
        }
    }

    private IReadOnlyCollection<IReadModelStore> ResolveReadModelStores(Type readModelType)
    {
        var readModelStoreType = typeof(IReadModelStore<>).MakeGenericType(readModelType);
        var readModelStores = _serviceProvider.GetServices(readModelStoreType)
            .Select(s => (IReadModelStore)s)
            .ToList();

        if (!readModelStores.Any())
        {
            throw new ArgumentException($"Could not find any read stores for read model '{readModelType.PrettyPrint()}'");
        }

        return readModelStores;
    }

    private IReadOnlyCollection<IReadStoreManager> ResolveReadStoreManagers(Type readModelType)
    {
        return _memoryCache.GetOrCreate(CacheKey.With(GetType(), readModelType.ToString(), nameof(ResolveReadStoreManagers)),
            e =>
            {
                var readStoreManagers = _serviceProvider.GetServices<IReadStoreManager>()
                .Where(m => m.ReadModelType == readModelType)
                .ToList();

                if (!readStoreManagers.Any())
                {
                    throw new ArgumentException($"Did not find any read store managers for read model type '{readModelType.PrettyPrint()}'");
                }

                return readStoreManagers;
            });
    }
}