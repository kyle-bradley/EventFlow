using EventFlow.Aggregates;
using EventFlow.Core;

namespace ClearFlow.StatePersistence.StateStores;

public abstract class EntityStateHydratedEvent<TAggregate, TIdentity> : AggregateEvent<TAggregate, TIdentity>, IEntityHydratedEvent
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
{
    public string IdentityId { get; }
    public IStateModel State { get; }

    public EntityStateHydratedEvent(string identityId, IStateModel state)
    {
        IdentityId = identityId;
        State = state;
    }

    public TModel GetState<TModel>()
        where TModel : class, IStateModel
    {
        return (TModel)State;
    }

    public IEntityHydratedEvent GetEvent()
    {
        return this;
    }
}
