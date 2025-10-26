using ClearFlow.StatePersistence.Aggregates;
using EventFlow.Aggregates;
using EventFlow.Core;

namespace ClearFlow.StatePersistence.StateStores;

public class AggregateStateUpdatedEvent<TAggregate, TIdentity, TState> : IAggregateStateUpdatedEvent<TAggregate, TIdentity>
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
        where TState : class, IStateModel
{
    public TrackedModel<TState> UpdatedState { get; }

    public AggregateStateUpdatedEvent(TrackedModel<TState> updatedState)
    {
        UpdatedState = updatedState;
    }
}
