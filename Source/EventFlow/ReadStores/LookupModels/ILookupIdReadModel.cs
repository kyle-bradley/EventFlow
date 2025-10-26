using EventFlow.Core;
using System.Collections.Generic;

namespace EventFlow.ReadStores.LookupModels;

public interface ILookupIdReadModel : IReadModel
{
    public string GetId<TTargetId>()
        where TTargetId : IIdentity;
    public string GetId(string targetId);
    public IReadOnlySet<string> GetIds<TTargetId>()
        where TTargetId : IIdentity;
    public IReadOnlySet<string> GetIds(string targetId);
}
