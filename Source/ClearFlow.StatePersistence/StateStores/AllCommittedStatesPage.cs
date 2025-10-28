using EventFlow.EventStores;
using System.Collections.Generic;

namespace ClearFlow.StatePersistence.StateStores;

public class AllCommittedStatesPage
{
    public GlobalPosition NextGlobalPosition { get; }
    public ICollection<IEntityHydratedEvent> StateHydratedEvents { get; }

    public AllCommittedStatesPage(
        GlobalPosition nextGlobalPosition,
        ICollection<IEntityHydratedEvent> stateHydratedEvents)
    {
        NextGlobalPosition = nextGlobalPosition;
        StateHydratedEvents = stateHydratedEvents;
    }
}
