using EventFlow.Core;
using System.Collections.Generic;

namespace EventFlow.ReadStores.LookupModels;

public interface ILocatorProcessor
{
    public string ProcessId<TLookupModel, TTargetId>(string identity)
       where TLookupModel : class, ILookupIdReadModel
       where TTargetId : IIdentity;

    public string ProcessId<TLookupModel, TTargetId>(IIdentity identity)
       where TLookupModel : class, ILookupIdReadModel
       where TTargetId : IIdentity;
    public string ProcessId<TLookupModel>(string targetId, string identity)
       where TLookupModel : class, ILookupIdReadModel;

    public IReadOnlySet<string> ProcessIds<TLookupModel, TTargetId>(string identity)
       where TLookupModel : class, ILookupIdReadModel
       where TTargetId : IIdentity;

    public IReadOnlySet<string> ProcessIds<TLookupModel, TTargetId>(IIdentity identity)
       where TLookupModel : class, ILookupIdReadModel
       where TTargetId : IIdentity;
    public IReadOnlySet<string> ProcessIds<TLookupModel>(string targetId, string identity)
       where TLookupModel : class, ILookupIdReadModel;
}
