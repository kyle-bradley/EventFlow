using ClearFlow.StatePersistence.StateStores;
using EventFlow.Aggregates;
using EventFlow.Core;
using EventFlow.EventStores;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClearFlow.StatePersistence.Aggregates;

public interface IStateStore
{
    Task<TrackedModel<TStateModel>> LoadStateAsync<TAggregate, TIdentity, TStateModel>(
        TIdentity identity,
        CancellationToken cancellationToken)
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
        where TStateModel : class, IStateModel;

    Task StoreStateAsync<TAggregate, TIdentity, TStateModel>(
        TIdentity identity,
        TrackedModel<TStateModel> model,
        CancellationToken cancellationToken)
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
        where TStateModel : class, IStateModel;

    Task<AllEventsPage> LoadAllStateEventsAsync(Type stateType,
            GlobalPosition globalPosition,
            int pageSize,
            CancellationToken cancellationToken);

    ICollection<Type> GetAllStateTypes { get; }
}
