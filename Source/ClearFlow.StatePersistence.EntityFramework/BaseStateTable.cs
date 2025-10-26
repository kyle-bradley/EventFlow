using ClearFlow.StatePersistence.StateStores;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClearFlow.StatePersistence.EntityFramework;

public abstract class BaseStateTable : IStateTable
{
    [Key]
    public string Id { get; set; }

    [ConcurrencyCheck]
    public long Version { get; set; } = 1;

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long GlobalSequenceNumber { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedTimestamp { get; set; } = DateTime.UtcNow;
}
