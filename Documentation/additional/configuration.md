title: Configuration
---

# EventFlow runtime configuration

EventFlow ships with sensible defaults, but most production workloads need to tune how the pipeline reacts to retries, subscriber failures, and replay throughput. All of those switches are exposed via `EventFlowOptions` when you wire the framework into your dependency injection container.

## How configuration is applied

Calling `AddEventFlow` registers the core services and gives you an `IEventFlowOptions` hook. Every configuration tweak happens inside that callback.

```csharp
using System;
using Microsoft.Extensions.DependencyInjection;
using EventFlow;

var services = new ServiceCollection();

services.AddEventFlow(options =>
{
    options
        .ConfigureOptimisticConcurrencyRetry(retries: 6, delayBeforeRetry: TimeSpan.FromMilliseconds(250))
        .Configure(cfg =>
        {
            cfg.ThrowSubscriberExceptions = true;
            cfg.IsAsynchronousSubscribersEnabled = true;
        });
});

using var provider = services.BuildServiceProvider();
```

Under the covers `AddEventFlow` calls `EventFlowOptions.New(serviceCollection)` and stores a single `EventFlowConfiguration` instance in the container as both `IEventFlowConfiguration` and `ICancellationConfiguration`. Additional fluent helpers (e.g., `.AddEvents`, `.AddCommands`, `.UsePostgreSqlEventStore`) can be chained in the same callback.

!!! tip
    You can invoke `.Configure(...)` multiple times. Each delegate receives the same `EventFlowConfiguration` instance, so later calls simply overwrite earlier values.

## `EventFlowConfiguration` reference

`EventFlowConfiguration` is defined in `Source/EventFlow/Configuration/EventFlowConfiguration.cs`. All properties are mutable so that they can be adjusted during startup.

| Setting | Default | Used by | Effect |
| --- | --- | --- | --- |
| `LoadReadModelEventPageSize` | `200` | `ReadModelPopulator.LoadEventsAsync` | Controls how many events are fetched per call when bulk-populating read models via `IReadModelPopulator`. Increase this if your event store can stream large pages efficiently; reduce it when replaying against constrained backends.
| `PopulateReadModelEventPageSize` | `10000` | `ReadModelPopulator.ProcessEventQueueAsync` | Sets the batch size used when dispatching replayed events to read-store managers. Lower values trade throughput for lower memory pressure during large replays.
| `NumberOfRetriesOnOptimisticConcurrencyExceptions` | `4` | `OptimisticConcurrencyRetryStrategy` | Upper bound on how many times the aggregate store retries commits when the persistence layer reports `OptimisticConcurrencyException`.
| `DelayBeforeRetryOnOptimisticConcurrencyExceptions` | `00:00:00.100` | `OptimisticConcurrencyRetryStrategy` | Delay inserted between those retries. Combine with the retry count to soften hot spots in high-contention aggregates.
| `ThrowSubscriberExceptions` | `false` | `DispatchToEventSubscribers` | When `false`, synchronous subscriber exceptions are logged and wrapped in a resilience strategy; when `true`, they are rethrown so the calling command handler observes the failure immediately.
| `IsAsynchronousSubscribersEnabled` | `false` | `DomainEventPublisher.PublishToAsynchronousSubscribersAsync` | When enabled, every asynchronous subscriber invocation is scheduled through `IJobScheduler` (`InstantJobScheduler` by default). Pair this with a durable scheduler such as `EventFlow.Hangfire` to honor delayed execution.
| `CancellationBoundary` | `CancellationBoundary.BeforeCommittingEvents` | `ICancellationConfiguration.Limit` | Decides how far cancellation tokens propagate through the command pipeline. Choose a later boundary if downstream infrastructure (read stores, subscribers) should respect cancellation requests.
| `ForwardOptimisticConcurrencyExceptions` | `false` | `AggregateStore` | When `true`, optimistic concurrency exceptions are forwarded to `IAggregateStoreResilienceStrategy.HandleCommitFailedAsync` before bubbling out. Use this if you implement a custom resilience strategy that can translate conflicts into domain-specific outcomes.

!!! note
    The enum values for `CancellationBoundary` are defined in `Configuration/Cancellation/CancellationBoundary.cs` and progress in chronological order through the command pipeline (`BeforeUpdatingAggregate` → `BeforeCommittingEvents` → `BeforeUpdatingReadStores` → `BeforeNotifyingSubscribers` → `CancelAlways`).

## Practical configuration scenarios

### Enable durable asynchronous subscribers

```csharp
using Hangfire;
using EventFlow.Hangfire.Extensions;

services.AddHangfire(config => config.UseInMemoryStorage());
services.AddHangfireServer();

services.AddEventFlow(options =>
{
    options.Configure(cfg =>
    {
        cfg.IsAsynchronousSubscribersEnabled = true;
    });

    options.UseHangfireJobScheduler();
});
```

Setting `IsAsynchronousSubscribersEnabled` causes `DomainEventPublisher` to enqueue a `DispatchToAsynchronousEventSubscribersJob` for every emitted domain event. Without a scheduler such as Hangfire, the bundled `InstantJobScheduler` executes jobs immediately in-process, effectively making asynchronous subscribers synchronous.

### Harden aggregates against hot-spot contention

```csharp
services.AddEventFlow(options =>
{
    options
        .ConfigureOptimisticConcurrencyRetry(retries: 8, delayBeforeRetry: TimeSpan.FromMilliseconds(500))
        .Configure(cfg => cfg.ForwardOptimisticConcurrencyExceptions = true);
});
```

The retry helper only adjusts the built-in retry strategy. Setting `ForwardOptimisticConcurrencyExceptions` allows a custom `IAggregateStoreResilienceStrategy` to inspect the conflict and, for example, emit a domain-specific execution result instead of throwing.

### Tune read model replay throughput

```csharp
services.AddEventFlow(options =>
{
    options.Configure(cfg =>
    {
        cfg.LoadReadModelEventPageSize = 1000;     // event store paging
        cfg.PopulateReadModelEventPageSize = 2000; // read model batch size
    });
});
```

These knobs directly influence `IReadModelPopulator.PopulateAsync`. Smaller batches reduce memory footprint and can help when replaying to remote databases; larger batches maximize throughput when the event store and read store are co-located.

### Adjust cancellation semantics

```csharp
services.AddEventFlow(options =>
{
    options.Configure(cfg =>
    {
        cfg.CancellationBoundary = CancellationBoundary.BeforeNotifyingSubscribers;
    });
});
```

Raising the boundary ensures cancellation tokens are honored while rebuilding read stores, but once the boundary is crossed EventFlow will run to completion to keep the event store and read models consistent.

## Consuming configuration at runtime

Every component registered with the container can request `IEventFlowConfiguration` or `ICancellationConfiguration` to observe these values.

```csharp
using EventFlow.Configuration;

public class ProjectionWorker(IEventFlowConfiguration configuration)
{
    public Task HandleAsync(CancellationToken cancellationToken)
    {
        var maxBatchSize = configuration.PopulateReadModelEventPageSize;
        // ... use the configured value
        return Task.CompletedTask;
    }
}
```

This is useful when custom infrastructure (for example, an outbox publisher) needs to stay in lockstep with the same retry and cancellation semantics as the built-in components.

## See also

- [Subscribers](../basics/subscribers.md) — explains synchronous vs. asynchronous subscribers in detail.
- [Queries and read stores](../basics/queries.md) and [Read store integrations](../integration/read-stores.md) — pair naturally with the read model replay settings.
- [Commands](../basics/commands.md) — outlines how command handlers surface execution results that may be impacted by retry and exception settings.
