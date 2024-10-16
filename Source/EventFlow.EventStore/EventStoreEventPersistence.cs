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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventFlow.Aggregates;
using EventFlow.Core;
using EventFlow.Exceptions;
using EventStore.Client;
using Microsoft.Extensions.Logging;

namespace EventFlow.EventStores.EventStore
{
    public class EventStoreEventPersistence : IEventPersistence
    {
        private readonly ILogger<EventStoreEventPersistence> _logger;
        private readonly EventStoreClient _client;

        private class EventStoreEvent : ICommittedDomainEvent
        {
            public string AggregateId { get; set; }
            public string Data { get; set; }
            public string Metadata { get; set; }
            public int AggregateSequenceNumber { get; set; }
        }

        public EventStoreEventPersistence(
            ILogger<EventStoreEventPersistence> log,
            EventStoreClient client)
        {
            _logger = log;
            _client = client;
        }

        public async Task<AllCommittedEventsPage> LoadAllCommittedEvents(
            GlobalPosition globalPosition,
            int pageSize,
            CancellationToken cancellationToken)
        {

            var nextPosition = ParsePosition(globalPosition);
            var resolvedEvents = new List<ResolvedEvent>();

            do
            {
                var pageSizeWithAnchor = pageSize + 1;
                var result = _client.ReadAllAsync(Direction.Forwards, nextPosition, pageSizeWithAnchor, cancellationToken: cancellationToken);
                var fullResult = await result.ToListAsync();

                if (!fullResult.Any())
                {
                    nextPosition = Position.End;
                    break;
                }

                var onlyAnchorEventRemains = fullResult.Count == 1;
                var eventsAvailableLessAnchorEvent = !onlyAnchorEventRemains ? fullResult.Count() - 1 : 1;

                var nonDeletedEvents = fullResult.Take(eventsAvailableLessAnchorEvent)
                    .Where(e => !(e.OriginalStreamId.StartsWith("$") || e.Event.EventType.StartsWith("$"))).ToList();

                resolvedEvents.AddRange(nonDeletedEvents);
                nextPosition = !onlyAnchorEventRemains ? fullResult.Last().OriginalPosition.Value : Position.End;
            }
            while (nextPosition != Position.End);

            var eventStoreEvents = Map(resolvedEvents);

            return new AllCommittedEventsPage(
                new GlobalPosition(string.Format("{0}-{1}", nextPosition.CommitPosition, nextPosition.PreparePosition)),
                eventStoreEvents);
        }

        private static Position ParsePosition(GlobalPosition globalPosition)
        {
            if (globalPosition.IsStart)
            {
                return Position.Start;
            }

            var parts = globalPosition.Value.Split('-');
            if (parts.Length != 2)
            {
                throw new ArgumentException(string.Format(
                    "Unknown structure for global position '{0}'. Expected it to be empty or in the form 'L-L'",
                    globalPosition.Value));
            }

            var commitPosition = ulong.Parse(parts[0]);
            var preparePosition = ulong.Parse(parts[1]);

            return new Position(commitPosition, preparePosition);
        }

        public async Task<IReadOnlyCollection<ICommittedDomainEvent>> CommitEventsAsync(
            IIdentity id,
            IReadOnlyCollection<SerializedEvent> serializedEvents,
            CancellationToken cancellationToken)
        {
            var committedDomainEvents = serializedEvents
                .Select(e => new EventStoreEvent
                    {
                        AggregateSequenceNumber = e.AggregateSequenceNumber,
                        Metadata = e.SerializedMetadata,
                        AggregateId = id.Value,
                        Data = e.SerializedData
                    })
                .ToList();

            var aggregateVersion = (ulong) serializedEvents.Min(e => e.AggregateSequenceNumber) - 2;
            var expectedVersion = aggregateVersion < 0 ? StreamRevision.None : new StreamRevision(aggregateVersion);
            var eventDatas = serializedEvents
                .Select(e =>
                    {
                        // While it might be tempting to use e.Metadata.EventId here, we can't
                        // as EventStore won't detect optimistic concurrency exceptions then
                        var guid = Uuid.NewUuid();

                        var eventType = string.Format("{0}.{1}.{2}", e.Metadata[MetadataKeys.AggregateName], e.Metadata.EventName, e.Metadata.EventVersion);
                        var data = Encoding.UTF8.GetBytes(e.SerializedData);
                        var meta = Encoding.UTF8.GetBytes(e.SerializedMetadata);
                        return new EventData(guid, eventType, data, meta);
                    })
                .ToList();

            try
            {
                var writeResult = await _client.AppendToStreamAsync(
                    id.Value,
                    expectedVersion,
                    eventDatas);

                _logger.LogTrace(
                    "Wrote entity {0} with version {1} ({2},{3})",
                    id,
                    writeResult.NextExpectedStreamRevision.ToUInt64() - 1,
                    writeResult.LogPosition.CommitPosition,
                    writeResult.LogPosition.PreparePosition);
            }
            catch (WrongExpectedVersionException e)
            {
                throw new OptimisticConcurrencyException(e.Message, e);
            }

            return committedDomainEvents;
        }

        public async Task<IReadOnlyCollection<ICommittedDomainEvent>> LoadCommittedEventsAsync(
            IIdentity id,
            int fromEventSequenceNumber,
            CancellationToken cancellationToken)
        {

            var startPosition = fromEventSequenceNumber <= 1
                ? StreamPosition.Start
                : StreamPosition.FromInt64(fromEventSequenceNumber - 1); // Starts from zero

            try
            {
                var result = _client.ReadStreamAsync(Direction.Forwards, id.Value, startPosition, cancellationToken: cancellationToken);
                var resolvedEvents = await result.ToListAsync();

                return Map(resolvedEvents);
            }
            catch (StreamNotFoundException)
            {
                return new List<ICommittedDomainEvent>();
            }
            catch (StreamDeletedException)
            {
                return new List<ICommittedDomainEvent>();
            }
        }

        public async Task<IReadOnlyCollection<ICommittedDomainEvent>> LoadCommittedEventsAsync(IIdentity id, int fromEventSequenceNumber, int toEventSequenceNumber, CancellationToken cancellationToken)
        {
            try
            {
                var startPosition = fromEventSequenceNumber <= 1
                    ? StreamPosition.Start
                    : StreamPosition.FromInt64(fromEventSequenceNumber - 1); // Starts from zero

                var result = _client.ReadStreamAsync(Direction.Forwards, id.Value, startPosition, cancellationToken: cancellationToken);
                var resolvedEvents = await result.Where(x => x.Event.EventNumber.ToInt64() <= toEventSequenceNumber - 1).ToListAsync();
                return Map(resolvedEvents);
            }
            catch (StreamNotFoundException)
            {
                return new List<ICommittedDomainEvent>();
            }
            catch (StreamDeletedException)
            {
                return new List<ICommittedDomainEvent>();
            }
        }

        public async Task DeleteEventsAsync(IIdentity id, CancellationToken cancellationToken)
        {
            var result = await _client.TombstoneAsync(id.Value, StreamState.Any);
        }

        private static IReadOnlyCollection<EventStoreEvent> Map(IEnumerable<ResolvedEvent> resolvedEvents)
        {
            return resolvedEvents
                .Select(e => new EventStoreEvent
                    {
                        AggregateSequenceNumber = (int)(e.Event.EventNumber.ToInt64() + 1), // Starts from zero
                        Metadata = Encoding.UTF8.GetString(e.Event.Metadata.ToArray()),
                        AggregateId = e.OriginalStreamId,
                        Data = Encoding.UTF8.GetString(e.Event.Data.ToArray()),
                    })
                .ToList();
        }
    }
}