using ClearFlow.StatePersistence.StateStores;
using EventFlow.Aggregates;
using EventFlow.Core;
using EventFlow.Extensions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;


namespace ClearFlow.StatePersistence.Aggregates;

public class TrackedAggregateState<TAggregate, TIdentity, TEventApplier, TStateModel> : IStatefulEventApplier<TAggregate, TIdentity, TStateModel>
        where TEventApplier : class, IEventApplier<TAggregate, TIdentity>
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
        where TStateModel : class, IStateModel, new()
{
    private static readonly IReadOnlyDictionary<Type, Action<TEventApplier, IAggregateEvent>> ApplyMethods;
    private TrackedModel<TStateModel> state;
    public TStateModel Model => state.Model;
    public TrackedModel<TStateModel> TrackedModel => state;

    static TrackedAggregateState()
    {
        ApplyMethods = typeof(TEventApplier).GetAggregateEventApplyMethods<TAggregate, TIdentity, TEventApplier>();
    }

    protected TrackedAggregateState()
    {
        var me = this as TEventApplier;
        if (me == null)
        {
            throw new InvalidOperationException(
                $"Event applier of type '{GetType().PrettyPrint()}' has a wrong generic argument '{typeof(TEventApplier).PrettyPrint()}'");
        }

        state = TrackedModel<TStateModel>.FromEmptyState(new TStateModel());
    }

    public void HydateState(TStateModel hydratedState, long version)
    {
        state = TrackedModel<TStateModel>.From(hydratedState, version);
    }

    protected void SetValue<TProperty>(Expression<Func<TStateModel, TProperty>> expression, TProperty value)
    {
        state.SetValue(expression, value);
    }
    protected void MarkForDeletion()
    {
        state.MarkForDeletion();
    }

    public bool Apply(
        TAggregate aggregate,
        IAggregateEvent<TAggregate, TIdentity> aggregateEvent)
    {
        var isUpdateEvent = aggregateEvent is IAggregateStateUpdatedEvent<TAggregate, TIdentity>;
        if (isUpdateEvent)
        {
            return true;
        }

        var aggregateEventType = aggregateEvent.GetType();

        Action<TEventApplier, IAggregateEvent> applier;

        if (!ApplyMethods.TryGetValue(aggregateEventType, out applier))
        {
            return false;
        }

        applier((TEventApplier)(object)this, aggregateEvent);
        return true;
    }
}
