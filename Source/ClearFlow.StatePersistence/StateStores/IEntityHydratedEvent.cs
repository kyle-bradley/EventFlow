using EventFlow.Aggregates;

namespace ClearFlow.StatePersistence.StateStores;

public interface IEntityHydratedEvent : IAggregateEvent
{
    public IStateModel State { get; }
    public string IdentityId { get; }
}
