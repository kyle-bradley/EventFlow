using EventFlow.Aggregates;
using EventFlow.Core;

namespace ClearFlow.StatePersistence.StateStores;

public class AggregateHydratedEvent<TAggregate, TIdentity, TState> : IAggregateEvent<TAggregate, TIdentity>
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
        where TState : class, IStateModel
{
    public TState HydratedState { get; }

    public AggregateHydratedEvent(TState hydratedState)
    {
        HydratedState = hydratedState;
    }
}
