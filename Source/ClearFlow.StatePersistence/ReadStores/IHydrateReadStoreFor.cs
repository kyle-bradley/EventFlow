using ClearFlow.StatePersistence.StateStores;
using EventFlow.Aggregates;
using EventFlow.Core;
using EventFlow.Queries;
using EventFlow.ReadStores;
using System.Collections.Generic;

namespace ClearFlow.StatePersistence.ReadStores;

public interface IHydrateReadStoreFor<TAggregate, TIdentity, THydratedEvent> : IAmReadModelFor<TAggregate, TIdentity, THydratedEvent>
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
        where THydratedEvent : EntityStateHydratedEvent<TAggregate, TIdentity>
{
}

