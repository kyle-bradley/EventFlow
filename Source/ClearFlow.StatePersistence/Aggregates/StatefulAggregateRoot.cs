using ClearFlow.StatePersistence.StateStores;
using EventFlow.Aggregates;
using EventFlow.Core;
using EventFlow.EventStores;
using EventFlow.Snapshots;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClearFlow.StatePersistence.Aggregates;

public abstract class StatefulAggregateRoot<TAggregate, TIdentity, TStateModel> : AggregateRoot<TAggregate, TIdentity>, IStatefulAggregateRoot<TIdentity>
        where TAggregate : StatefulAggregateRoot<TAggregate, TIdentity, TStateModel>
        where TIdentity : IIdentity
        where TStateModel : class, IStateModel, new()
{

    public StatefulAggregateRoot(TIdentity id) : base(id)
    { }

    public async Task LoadAsync(
        IEventStore eventStore,
        ISnapshotStore snapshotStore,
        IStateStore stateStore,
        CancellationToken cancellationToken)
    {
        var stateModel = await stateStore.LoadStateAsync<TAggregate, TIdentity, TStateModel>(
            Id,
            cancellationToken)
            .ConfigureAwait(false);

        Version = Convert.ToInt32(stateModel.Version);

        if (!stateModel.IsNew)
        {
            _stateHydateAppliers.ForEach(applier => applier.HydateState(stateModel.Model, stateModel.Version));
        }
    }

    public async Task<IReadOnlyCollection<IDomainEvent>> CommitAsync(
        IEventStore eventStore,
        ISnapshotStore snapshotStore,
        IStateStore stateStore,
        ISourceId sourceId,
        CancellationToken cancellationToken)
    {
        EmitState();

        var noStatesEmitted = !UncommittedEvents.Any(e => e.AggregateEvent is IAggregateStateUpdatedEvent<TAggregate, TIdentity>);
        if (noStatesEmitted)
        {
            throw new InvalidOperationException("No state event emmited. Please emit the update event using AggregateStateUpdatedEvent");
        }

        var domainEvents = await base.CommitAsync(eventStore, snapshotStore, sourceId, cancellationToken).ConfigureAwait(false);

        return domainEvents;
    }

    private readonly List<IStatefulEventApplier<TAggregate, TIdentity, TStateModel>> _stateHydateAppliers = new List<IStatefulEventApplier<TAggregate, TIdentity, TStateModel>>();

    protected void Register(IStatefulEventApplier<TAggregate, TIdentity, TStateModel> eventApplier)
    {
        _stateHydateAppliers.Add(eventApplier);
        base.Register(eventApplier);
    }

    protected abstract void EmitState();
}
