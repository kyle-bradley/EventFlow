using EventFlow.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EventFlow.ReadStores.LookupModels;

public abstract class LookupReadModel
{
    public Dictionary<string, HashSet<string>> MappedIds { get; set; }
    public LookupReadModel()
    {
        MappedIds = new Dictionary<string, HashSet<string>>();
    }

    public void SetSupportedTarget<TTargetId>()
        where TTargetId : IIdentity
    {
        var referenceType = typeof(TTargetId).Name;
        MappedIds[referenceType] = new HashSet<string>();
    }

    public void SetId<TTargetId>(string id)
        where TTargetId : IIdentity
    {
        var referenceType = GetSupportedType<TTargetId>();

        MappedIds[referenceType].Add(id);
    }

    public void SetIds<TTargetId>(HashSet<string> ids)
        where TTargetId : IIdentity
    {
        var referenceType = GetSupportedType<TTargetId>();

        MappedIds[referenceType] = MappedIds[referenceType].Union(ids.Select(id => id)).ToHashSet();
    }

    public void SetId<TTargetId>(IIdentity id)
        where TTargetId : IIdentity
    {
        SetId<TTargetId>(id.Value);
    }

    public void SetIds<TTargetId>(HashSet<IIdentity> ids)
        where TTargetId : IIdentity
    {
        SetIds<TTargetId>(ids.Select(id => id.Value).ToHashSet());
    }

    public string GetId<TTargetId>()
        where TTargetId : IIdentity
    {
        var referenceType = GetSupportedType<TTargetId>();
        return MappedIds[referenceType].Single();
    }

    public IReadOnlySet<string> GetIds<TTargetId>()
        where TTargetId : IIdentity
    {
        var referenceType = GetSupportedType<TTargetId>();
        return MappedIds[referenceType];
    }

    private string GetSupportedType<TTargetId>()
        where TTargetId : IIdentity
    {
        var referenceType = typeof(TTargetId).Name;
        if (!MappedIds.ContainsKey(referenceType))
        {
            throw new ArgumentOutOfRangeException(nameof(TTargetId), $"Type {referenceType} is not a supported lookup target. Please add it using SetSupportedTarget");
        }

        return referenceType;
    }
}
