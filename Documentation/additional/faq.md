---
title: FAQ
---

# FAQ - frequently asked questions

## How can I ensure that only specific users can execute commands?

EventFlow deliberately keeps the command pipeline thin. The default `CommandBus.PublishAsync` resolves the single `ICommandHandler<,,,>` that matches a command and forwards the call to `IAggregateStore.UpdateAsync`. No authorization hooks are executed for you.

You therefore have to enforce authorization either close to the domain or by decorating the command bus:

- Inject any ambient context (for example an `ICurrentUser`) into your command handlers and return a failed `IExecutionResult` or throw a `DomainError` if the caller is not allowed to proceed.
- Replace the `ICommandBus` registration with a decorator that checks permissions before delegating to the inner bus. EventFlow registers the bus with `TryAddTransient<ICommandBus, CommandBus>()`, so a subsequent `services.Replace(...)` takes over cleanly. One possible decorator looks like this:

```csharp
public sealed class SecuredCommandBus : ICommandBus
{
    private readonly ICommandBus _inner;
    private readonly ICommandAuthorizer _authorizer;
    private readonly ICurrentUser _user;

    public SecuredCommandBus(ICommandBus inner, ICommandAuthorizer authorizer, ICurrentUser user)
    {
        _inner = inner;
        _authorizer = authorizer;
        _user = user;
    }

    public Task<TExecutionResult> PublishAsync<TAggregate, TIdentity, TExecutionResult>(
        ICommand<TAggregate, TIdentity, TExecutionResult> command,
        CancellationToken cancellationToken)
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
        where TExecutionResult : IExecutionResult
    {
        if (!_authorizer.CanExecute(command, _user))
        {
            throw DomainError.With(
                "Command {0} is not allowed for {1}",
                command.GetType().Name,
                _user.Id);
        }

        return _inner.PublishAsync(command, cancellationToken);
    }
}
```

```csharp
services.AddEventFlow(options => { /* configure aggregates, commands, ... */ });

services.Replace(ServiceDescriptor.Transient<ICommandBus>(sp =>
    new SecuredCommandBus(
        ActivatorUtilities.CreateInstance<CommandBus>(sp),
        sp.GetRequiredService<ICommandAuthorizer>(),
        sp.GetRequiredService<ICurrentUser>())));
```

This keeps sensitive logic centralized while still letting the built-in `CommandBus` discover handlers and persist aggregates.

## Why isn't there a "global sequence number" on domain events?

Every `IDomainEvent` emitted by an aggregate exposes the `AggregateSequenceNumber` from `DomainEvent<TAggregate, TIdentity, TAggregateEvent>` and repeats the value in metadata under `MetadataKeys.AggregateSequenceNumber`. EventFlow guarantees ordering inside a single aggregate root and stops there, because cross-aggregate ordering is a projection concern rather than a domain invariant.

Most event store integrations (e.g., `MsSqlEventPersistence`, `PostgresSqlEventPersistence`, `SQLiteEventPersistence`) maintain their own `GlobalSequenceNumber` internally so they can serve `IEventStore.LoadAllEventsAsync(...)`. That API returns an `AllEventsPage` with a `GlobalPosition` token you can persist if you are building a log-reading process. Once the events are materialized into `IDomainEvent` instances and dispatched to handlers, the global number is intentionally not exposed—reactive code should not rely on cross-aggregate ordering promises.

If you need an application-level notion of global ordering, capture the `GlobalPosition` when you read from `LoadAllEventsAsync` or store it alongside your read model state.

## Why doesn't EventFlow have a unit of work concept?

The aggregate itself is the unit of consistency in EventFlow. A command published through the `CommandBus` flows into `IAggregateStore.UpdateAsync`, which (1) rehydrates the aggregate by replaying its event stream, (2) executes your domain logic delegate, (3) commits the newly emitted events in a single call to `IEventPersistence.CommitEventsAsync`, and (4) publishes the resulting domain events to read stores, subscribers, and sagas. Because aggregate state comes entirely from events, there is no ambient change tracker to flush and a classic unit-of-work abstraction would not add any extra safety.

When you really need to coordinate with another resource (e.g., enlist additional SQL statements), plug into `IAggregateStoreResilienceStrategy` or move the extra work into a subscriber that runs after the events have been durably written.

## Why are subscribers receiving events out of order?

EventFlow publishes events in stages through `DomainEventPublisher`:

- Read stores and synchronous subscribers run one event at a time and in order. `DispatchToEventSubscribers.DispatchToSynchronousSubscribersAsync` awaits each handler before moving on.
- Asynchronous subscribers are intentionally different. Each `IDomainEvent` is wrapped in a `DispatchToAsynchronousEventSubscribersJob` and scheduled via `IJobScheduler`. The default `InstantJobScheduler` executes the jobs immediately, but `DomainEventPublisher` starts them with `Task.WhenAll(...)`, so completion order is not guaranteed and alternative schedulers (such as Hangfire) may execute them on other workers entirely.

If your projection relies on strict ordering, keep it synchronous or persist enough information—such as the `AggregateSequenceNumber`—to detect and discard late arrivals.
