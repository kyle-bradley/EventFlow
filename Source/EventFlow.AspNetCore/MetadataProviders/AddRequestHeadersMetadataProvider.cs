// The MIT License (MIT)
// 
// Copyright (c) 2015-2024 Rasmus Mikkelsen
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

using System;
using System.Collections.Generic;
using System.Linq;
using EventFlow.Aggregates;
using EventFlow.Core;
using EventFlow.EventStores;
using Microsoft.AspNetCore.Http;

namespace EventFlow.AspNetCore.MetadataProviders
{
    public class AddRequestHeadersMetadataProvider : IMetadataProvider
    {
        private static readonly ISet<string> RequestHeadersToSkip = new HashSet<string>
        {
            "Authorization",
            "Cookie"
        };

        private readonly IHttpContextAccessor _httpContextAccessor;

        public AddRequestHeadersMetadataProvider(
            IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public IEnumerable<KeyValuePair<string, string>> ProvideMetadata<TAggregate, TIdentity>(
            TIdentity id,
            IAggregateEvent aggregateEvent,
            IMetadata metadata)
            where TAggregate : IAggregateRoot<TIdentity>
            where TIdentity : IIdentity
        {
            return _httpContextAccessor.HttpContext?.Request.Headers
                       .Where(kv => !RequestHeadersToSkip.Contains(kv.Key))
                       .Select(kv => new KeyValuePair<string, string>(
                           $"request_header[{kv.Key}]",
                           string.Join(Environment.NewLine, kv.Value)))
                   ?? Enumerable.Empty<KeyValuePair<string, string>>();
        }
    }
}
