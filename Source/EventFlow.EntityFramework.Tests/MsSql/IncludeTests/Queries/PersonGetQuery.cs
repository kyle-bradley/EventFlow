﻿// The MIT License (MIT)
// 
// Copyright (c) 2015-2023 Rasmus Mikkelsen
// Copyright (c) 2015-2020 eBay Software Foundation
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

using System.Threading;
using System.Threading.Tasks;
using EventFlow.EntityFramework.Tests.Model;
using EventFlow.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.EntityFramework.Tests.MsSql.IncludeTests.Queries
{
    public class PersonGetQuery : IQuery<Person>
    {
        public PersonId PersonId { get; }

        public PersonGetQuery(PersonId personId)
        {
            PersonId = personId;
        }
    }

    public class PersonGetQueryHandler : IQueryHandler<PersonGetQuery, Person>
    {
        private readonly IDbContextProvider<TestDbContext> _dbContextProvider;

        public PersonGetQueryHandler(IDbContextProvider<TestDbContext> dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task<Person> ExecuteQueryAsync(PersonGetQuery query, CancellationToken cancellationToken)
        {
            await using var context = _dbContextProvider.CreateContext();
            var entity = await context.Persons
                .Include(x => x.Addresses)
                .SingleOrDefaultAsync(x => x.AggregateId == query.PersonId.Value, cancellationToken)
                .ConfigureAwait(false);
            return entity?.ToPerson();
        }
    }
}