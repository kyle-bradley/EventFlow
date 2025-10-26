using EventFlow.Core;
using EventFlow.Queries;
using System;
using System.Collections.Generic;

namespace EventFlow.ReadStores.LookupModels;

public class LookupModelIdsQuery<TLookupModel, TTargetId> : IQuery<IReadOnlySet<string>>
       where TLookupModel : class, ILookupIdReadModel
       where TTargetId : IIdentity
{
    public string Id { get; }

    public LookupModelIdsQuery(IIdentity identity)
        : this(identity.Value)
    {
    }

    public LookupModelIdsQuery(string id)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

        Id = id;
    }
}

public class LookupModelIdsQuery<TLookupModel> : IQuery<IReadOnlySet<string>>
       where TLookupModel : class, ILookupIdReadModel
{
    public string Id { get; }
    public string TargetId { get; }

    public LookupModelIdsQuery(string targetId, IIdentity identity)
        : this(targetId, identity.Value)
    {
    }

    public LookupModelIdsQuery(string targetId, string id)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

        Id = id;
        TargetId = targetId;
    }
}
