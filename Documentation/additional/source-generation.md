---
title: Source Generation
---

# Source Generation

EventFlow provides source generators to reduce boilerplate code when working with aggregates. This makes your code cleaner and easier to maintain.

## How to use

Add [EventFlow.SourceGenerators](https://www.nuget.org/packages/EventFlow.SourceGenerators) package.

``` sh
dotnet add package EventFlow.SourceGenerators
```

Add the `AggregateExtensions` attribute to your aggregate class. The C# compiler will automatically generate the necessary source code.

``` csharp
using EventFlow.SourceGenerators;

[AggregateExtensions]
public class OrderAggregate(OrderAggregateId id) :
    AggregateRoot<OrderAggregate, OrderAggregateId>(id)
{
    public Task DoSomething()
    {
        // Business logic here
    }
}

public class OrderAggregateId(string value) : Identity<OrderAggregateId>(value);
```

## How it helps

Without source generators, you must specify the aggregate type and identity type explicitly in multiple places.

1. Event declaration.

    ``` csharp
    public class OrderCreated : 
        IAggregateEvent<OrderAggregate, OrderAggregateId>
    ```

2. Subscriber declaration.

    ``` csharp
    public class OrderAggregateSubscribers :
        ISubscribeSynchronousTo<OrderAggregate, OrderAggregateId, OrderCreated>
    ```

3. Usage of `IAggregateStore`.

    ``` csharp
    await aggregateStore.UpdateAsync<OrderAggregate, OrderAggregateId>(
        id,
        SourceId.New,
        (order, _) => order.DoSomething(),
        cancellationToken);
    ```

4. Usage of `IEventStore`.

    ``` csharp
    var events = await eventStore.LoadEventsAsync<OrderAggregate, OrderAggregateId>(
        id, cancellationToken);
    ```

Using source generators simplifies the code by omitting the aggregate and identity types.

1. Event declaration.

    ``` csharp
    public class OrderCreated : OrderAggregateEvent
    ```

2. Subscriber declaration.

    ``` csharp
    public class OrderAggregateSubscribers :
        ISubscribeSynchronousTo<OrderCreated>
    ```

3. Usage of `IAggregateStore`.

    ```csharp
    await aggregateStore.UpdateAsync(
        id,
        order => order.DoSomething(),
        cancellationToken);
    ```

4. Usage of `IEventStore`.

    ```csharp
    var events = await eventStore.LoadEventsAsync(id, cancellationToken);
    ```
