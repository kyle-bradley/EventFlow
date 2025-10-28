using EventFlow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using EventFlow.Aggregates;
using EventFlow.ReadStores;
using EventFlow.Core;
using EventFlow.Subscribers;
using ClearFlow.StatePersistence.StateStores;
using ClearFlow.StatePersistence.Aggregates;

namespace ClearFlow.StatePersistence.Extensions;
public static class EventFlowOptionsStatefulExtensions
{
    public static IEventFlowOptions AddStatefulAggregates(
            this IEventFlowOptions eventFlowOptions)
    {
        return eventFlowOptions.ConfigureStatefulOptions();
    }

    public static IEventFlowOptions UseStatePersistence<TAggregate, TIdentity, TState, TEvent, TPersistence>(
            this IEventFlowOptions eventFlowOptions)
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
        where TState : class, IStateModel
        where TPersistence : IStatePersistence<TState>
        where TEvent : AggregateStateUpdatedEvent<TAggregate, TIdentity, TState>
    {
        var configuredOptions = eventFlowOptions;

        configuredOptions.ServiceCollection
            .Add(ServiceDescriptor.Describe(typeof(IStatePersistence), typeof(TPersistence), ServiceLifetime.Transient));
        configuredOptions.ServiceCollection
            .Replace(ServiceDescriptor.Describe(typeof(IStatePersistence<TState>), typeof(TPersistence), ServiceLifetime.Transient))

            .Add(ServiceDescriptor.Describe(typeof(ISubscribeSynchronousTo<TAggregate, TIdentity, TEvent>),
            typeof(AggregateStatePersistenceSubscriber<TAggregate, TIdentity, TState, TEvent>), ServiceLifetime.Transient));

        return configuredOptions;
    }

    private static IEventFlowOptions ConfigureStatefulOptions(this IEventFlowOptions eventFlowOptions)
    {
        var configuredOptions = eventFlowOptions;

        configuredOptions.ServiceCollection
            .Replace(ServiceDescriptor.Describe(typeof(IReadModelContextFactory), typeof(ReadModelContextFactory), ServiceLifetime.Transient))
            .Replace(ServiceDescriptor.Describe(typeof(IAggregateStore), typeof(StatefulAggregateStore), ServiceLifetime.Transient))
            .Replace(ServiceDescriptor.Describe(typeof(IStateStore), typeof(StateStore), ServiceLifetime.Transient));

        return configuredOptions;
    }
}
