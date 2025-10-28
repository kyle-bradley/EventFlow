using EventFlow.Core;
using EventFlow.Queries;
using System.Collections.Generic;
using System.Threading;

namespace EventFlow.ReadStores.LookupModels;
public class LocatorProcessor : ILocatorProcessor
{
    private readonly IQueryProcessor queryProcessor;

    public LocatorProcessor(IQueryProcessor queryProcessor)
    {
        this.queryProcessor = queryProcessor;
    }

    public string ProcessId<TLookupModel, TTargetId>(IIdentity identity)
       where TLookupModel : class, ILookupIdReadModel
       where TTargetId : IIdentity
    {
        return ProcessId<TLookupModel, TTargetId>(identity.Value);
    }

    public string ProcessId<TLookupModel, TTargetId>(string identity)
       where TLookupModel : class, ILookupIdReadModel
       where TTargetId : IIdentity
    {
        var query = new LookupModelIdQuery<TLookupModel, TTargetId>(identity);

        return queryProcessor.ProcessAsync(query, CancellationToken.None).Result;
    }

    public string ProcessId<TLookupModel>(string targetId, string identity)
       where TLookupModel : class, ILookupIdReadModel
    {
        var query = new LookupModelIdQuery<TLookupModel>(targetId, identity);

        return queryProcessor.ProcessAsync(query, CancellationToken.None).Result;
    }

    public IReadOnlySet<string> ProcessIds<TLookupModel, TTargetId>(IIdentity identity)
       where TLookupModel : class, ILookupIdReadModel
       where TTargetId : IIdentity
    {
        return ProcessIds<TLookupModel, TTargetId>(identity.Value);
    }

    public IReadOnlySet<string> ProcessIds<TLookupModel, TTargetId>(string identity)
       where TLookupModel : class, ILookupIdReadModel
       where TTargetId : IIdentity
    {
        var query = new LookupModelIdsQuery<TLookupModel, TTargetId>(identity);

        return queryProcessor.ProcessAsync(query, CancellationToken.None).Result;
    }

    public IReadOnlySet<string> ProcessIds<TLookupModel>(string targetId, string identity)
       where TLookupModel : class, ILookupIdReadModel
    {
        var query = new LookupModelIdsQuery<TLookupModel>(targetId, identity);

        return queryProcessor.ProcessAsync(query, CancellationToken.None).Result;
    }
}