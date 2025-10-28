using ClearFlow.StatePersistence.Aggregates;
using EventFlow.Core;
using EventFlow.EventStores;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClearFlow.StatePersistence.StateStores;
public interface IStatePersistence
{
    public Type StateType { get; }
    Task<AllCommittedStatesPage> LoadAllCommittedEvents(GlobalPosition globalPosition, int pageSize, CancellationToken cancellationToken);
}

public interface IStatePersistence<TStateReadModel> : IStatePersistence
        where TStateReadModel : class, IStateModel
{
    Task<TStateReadModel> GetOrCreateAsync(string id, CancellationToken cancellationToken);
    Task<TStateReadModel> FindAsync(Func<TStateReadModel, bool> expression, CancellationToken cancellationToken);
    Task<TStateReadModel> GetAsync(IIdentity id, CancellationToken cancellationToken);
    Task<TrackedModel<TStateReadModel>> GetOrCreateAsync(IIdentity id, CancellationToken cancellationToken);
    Task UpsertAsync(IIdentity id, TrackedModel<TStateReadModel> model, CancellationToken cancellationToken);
    Task DeleteAsync(IIdentity id, CancellationToken cancellationToken);
}
