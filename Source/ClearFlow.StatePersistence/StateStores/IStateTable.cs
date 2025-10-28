using System;

namespace ClearFlow.StatePersistence.StateStores;

public interface IStateTable
{
    public string Id { get; set; }
    public long Version { get; set; }
    public long GlobalSequenceNumber { get; set; }
    public DateTime CreatedTimestamp { get; set; }
}
