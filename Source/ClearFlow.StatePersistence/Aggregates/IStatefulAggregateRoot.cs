using EventFlow.Aggregates;
using EventFlow.Core;
using EventFlow.EventStores;
using EventFlow.Snapshots;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClearFlow.StatePersistence.Aggregates;

public interface IStatefulAggregateRoot : IAggregateRoot
{
    Task LoadAsync(
        IEventStore eventStore,
        ISnapshotStore snapshotStore,
        IStateStore stateStore,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<IDomainEvent>> CommitAsync(
        IEventStore eventStore,
        ISnapshotStore snapshotStore,
        IStateStore stateStore,
        ISourceId sourceId,
        CancellationToken cancellationToken);
}


public interface IStatefulAggregateRoot<out TIdentity> : IAggregateRoot<TIdentity>, IStatefulAggregateRoot
    where TIdentity : IIdentity
{
}
