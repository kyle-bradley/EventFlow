using EventFlow.Core;
using EventFlow.Queries;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EventFlow.ReadStores.LookupModels;

public class LookupModelIdQueryHandler<TReadStore, TLookupModel, TTargetId> : 
    IQueryHandler<LookupModelIdQuery<TLookupModel, TTargetId>, string>, 
    IQueryHandler<LookupModelIdsQuery<TLookupModel, TTargetId>, IReadOnlySet<string>>,
    IQueryHandler<LookupModelIdQuery<TLookupModel>, string>,
    IQueryHandler<LookupModelIdsQuery<TLookupModel>, IReadOnlySet<string>>
    where TReadStore : IReadModelStore<TLookupModel>
    where TLookupModel : class, ILookupIdReadModel
    where TTargetId : IIdentity
{
    private readonly TReadStore _readStore;

    public LookupModelIdQueryHandler(TReadStore readStore)
    {
        _readStore = readStore;
    }

    public async Task<string> ExecuteQueryAsync(LookupModelIdQuery<TLookupModel, TTargetId> query, CancellationToken cancellationToken)
    {
        var readModelEnvelope = await _readStore.GetAsync(query.Id, cancellationToken).ConfigureAwait(false);
        var model = readModelEnvelope.ReadModel;

        return model.GetId<TTargetId>();
    }

    public async Task<IReadOnlySet<string>> ExecuteQueryAsync(LookupModelIdsQuery<TLookupModel, TTargetId> query, CancellationToken cancellationToken)
    {
        var readModelEnvelope = await _readStore.GetAsync(query.Id, cancellationToken).ConfigureAwait(false);
        var model = readModelEnvelope.ReadModel;

        return model.GetIds<TTargetId>();
    }

    public async Task<string> ExecuteQueryAsync(LookupModelIdQuery<TLookupModel> query, CancellationToken cancellationToken)
    {
        var readModelEnvelope = await _readStore.GetAsync(query.Id, cancellationToken).ConfigureAwait(false);
        var model = readModelEnvelope.ReadModel;

        return model.GetId(query.TargetId);
    }

    public async Task<IReadOnlySet<string>> ExecuteQueryAsync(LookupModelIdsQuery<TLookupModel> query, CancellationToken cancellationToken)
    {
        var readModelEnvelope = await _readStore.GetAsync(query.Id, cancellationToken).ConfigureAwait(false);
        var model = readModelEnvelope.ReadModel;

        return model.GetIds(query.TargetId);
    }
}