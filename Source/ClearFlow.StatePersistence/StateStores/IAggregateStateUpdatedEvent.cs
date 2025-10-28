using EventFlow.Aggregates;
using EventFlow.Core;

namespace ClearFlow.StatePersistence.StateStores;

public interface IAggregateStateUpdatedEvent<TAggregate, TIdentity> : IAggregateEvent<TAggregate, TIdentity>
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
{
}
