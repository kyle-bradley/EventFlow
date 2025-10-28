using EventFlow.Core;
using EventFlow.Queries;
using System;

namespace EventFlow.ReadStores.LookupModels;

public class LookupModelIdQuery<TLookupModel, TTargetId> : IQuery<string>
       where TLookupModel : class, ILookupIdReadModel
       where TTargetId : IIdentity
{
    public string Id { get; }

    public LookupModelIdQuery(IIdentity identity)
        : this(identity.Value)
    {
    }

    public LookupModelIdQuery(string id)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

        Id = id;
    }
}

public class LookupModelIdQuery<TLookupModel> : IQuery<string>
       where TLookupModel : class, ILookupIdReadModel
{
    public string Id { get; }
    public string TargetId { get; }

    public LookupModelIdQuery(string targetId, IIdentity identity)
        : this(targetId, identity.Value)
    { }

    public LookupModelIdQuery(string targetId, string id)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

        Id = id;
        TargetId = targetId;
    }
}
