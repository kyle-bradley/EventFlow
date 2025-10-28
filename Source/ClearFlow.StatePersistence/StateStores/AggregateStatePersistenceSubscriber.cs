using ClearFlow.StatePersistence.Aggregates;
using EventFlow.Aggregates;
using EventFlow.Core;
using EventFlow.Subscribers;
using System.Threading;
using System.Threading.Tasks;

namespace ClearFlow.StatePersistence.StateStores;

public class AggregateStatePersistenceSubscriber<TAggregate, TIdentity, TState, TEvent> : ISubscribeSynchronousTo<TAggregate, TIdentity, TEvent>
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
        where TState : class, IStateModel
        where TEvent : AggregateStateUpdatedEvent<TAggregate, TIdentity, TState>
{
    private readonly IStateStore stateStore;

    public AggregateStatePersistenceSubscriber(IStateStore stateStore)
    {
        this.stateStore = stateStore;
    }

    public async Task HandleAsync(IDomainEvent<TAggregate, TIdentity, TEvent> domainEvent, CancellationToken cancellationToken)
    {
        await stateStore.StoreStateAsync<TAggregate, TIdentity, TState>(domainEvent.AggregateIdentity, domainEvent.AggregateEvent.UpdatedState, cancellationToken);
    }
}