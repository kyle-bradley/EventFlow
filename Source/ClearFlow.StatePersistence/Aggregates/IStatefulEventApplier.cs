using ClearFlow.StatePersistence.StateStores;
using EventFlow.Aggregates;
using EventFlow.Core;

namespace ClearFlow.StatePersistence.Aggregates;

public interface IStatefulEventApplier<TAggregate, TIdentity, TStateModel> : IEventApplier<TAggregate, TIdentity>
    where TAggregate : IAggregateRoot<TIdentity>
    where TIdentity : IIdentity
    where TStateModel : class, IStateModel
{
    public void HydateState(TStateModel hydratedState, long version);
}
