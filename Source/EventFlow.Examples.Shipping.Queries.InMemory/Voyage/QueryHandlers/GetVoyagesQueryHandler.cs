// The MIT License (MIT)
// 
// Copyright (c) 2015-2023 Rasmus Mikkelsen
// Copyright (c) 2015-2021 eBay Software Foundation
// https://github.com/eventflow/EventFlow
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventFlow.Examples.Shipping.Domain.Model.VoyageModel;
using EventFlow.Examples.Shipping.Domain.Model.VoyageModel.Queries;
using EventFlow.Queries;
using EventFlow.ReadStores.InMemory;

namespace EventFlow.Examples.Shipping.Queries.InMemory.Voyage.QueryHandlers
{
    public class GetVoyagesQueryHandler : IQueryHandler<GetVoyagesQuery, IReadOnlyCollection<Domain.Model.VoyageModel.Voyage>>
    {
        private readonly IInMemoryReadStore<VoyageReadModel> _readStore;

        public GetVoyagesQueryHandler(
            IInMemoryReadStore<VoyageReadModel> readStore)
        {
            _readStore = readStore;
        }

        public async Task<IReadOnlyCollection<Domain.Model.VoyageModel.Voyage>> ExecuteQueryAsync(GetVoyagesQuery query, CancellationToken cancellationToken)
        {
            var voyageIds = new HashSet<VoyageId>(query.VoyageIds);
            var voyageReadModels = await _readStore.FindAsync(rm => voyageIds.Contains(rm.Id), cancellationToken).ConfigureAwait(false);
            return voyageReadModels.Select(rm => rm.ToVoyage()).ToList();
        }
    }
}