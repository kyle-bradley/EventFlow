---
layout: default
title: MongoDB
parent: Integration
nav_order: 3
---

# MongoDB

Use the `EventFlow.MongoDB` integration when you want EventFlow to persist events,
read models, or snapshots in MongoDB. This guide walks through the recommended
package, configuration patterns, collection preparation, and a few
troubleshooting tips.

## Prerequisites

- A MongoDB server (Replica Set recommended for production). EventFlow works with
	MongoDB 5.0 or newer; the integration tests run against Mongo2Go, which ships
	with MongoDB 6.x.
- A .NET application already wired with `EventFlow`.
- Network access and credentials that allow reads and writes to the target database.

## Install the NuGet package

Add the MongoDB integration to every project that configures EventFlow.

```bash
dotnet add package EventFlow.MongoDB
```

## Configure EventFlow

The `ConfigureMongoDb` helpers make sure a single `IMongoDatabase` instance is
registered with DI. You can pass a connection string, a custom `MongoClient`, or
an `IMongoDatabase` factory.

```csharp
// Program.cs / Startup.cs
var mongoUrl = new MongoUrl(Configuration.GetConnectionString("eventflow-mongo"));
var mongoClient = new MongoClient(mongoUrl);

services.AddEventFlow(ef => ef
		.ConfigureMongoDb(mongoClient, mongoUrl.DatabaseName)
		.UseMongoDbEventStore()                          // Events
		.UseMongoDbSnapshotStore()                       // Snapshots (optional)
		.UseMongoDbReadModel<UserReadModel>()            // Read models
		.UseMongoDbReadModel<UserNicknameReadModel, UserNicknameLocator>());
```

### Read models must implement `IMongoDbReadModel`

Mongo-backed read models use optimistic concurrency on a `Version` field and
store documents in a single collection per read model type. Implement the
interface and optionally override the collection name.

```csharp
[MongoDbCollectionName("users")]
public class UserReadModel : IMongoDbReadModel,
		IAmReadModelFor<UserAggregate, UserId, UserCreated>
{
		public string Id { get; set; } = default!;        // MongoDB document _id
		public long? Version { get; set; }
		public string Username { get; set; } = default!;

		public Task ApplyAsync(
				IReadModelContext context,
				IDomainEvent<UserAggregate, UserId, UserCreated> domainEvent,
				CancellationToken cancellationToken)
		{
				Id = domainEvent.AggregateIdentity.Value;
				Username = domainEvent.AggregateEvent.Username.Value;
				return Task.CompletedTask;
		}
}
```

If you omit `MongoDbCollectionNameAttribute`, EventFlow defaults to
`ReadModel-[TypeName]` for the collection name.

### Snapshots

Calling `UseMongoDbSnapshotStore()` stores aggregate snapshots in the same
database. Each snapshot is kept in a shared `eventflow-snapshots` collection,
including the version number and metadata required for upgrades.

## Prepare collections and indexes

EventFlow registers an `IMongoDbEventPersistenceInitializer` that sets up the
unique index on `(AggregateId, AggregateSequenceNumber)` in the events
collection. Run it once during application startup or as a migration step.

```csharp
using (var scope = services.BuildServiceProvider().CreateScope())
{
		scope.ServiceProvider
				.GetRequiredService<IMongoDbEventPersistenceInitializer>()
				.Initialize();
}
```

Read model collections are created lazily. When running in production, pre-create
them with the appropriate indexes for your query workload (for example, on
`Username` or `TenantId` fields) and size any capped collections ahead of time.

## Local development quickstart

Spin up a disposable MongoDB container and point your connection string at
`mongodb://localhost:27017/eventflow`.

```bash
docker run --rm -p 27017:27017 --name eventflow-mongo mongo:7
```

Integration tests live in `Source/EventFlow.MongoDB.Tests` if you need sample
fixtures for seeding data or running smoke tests.

## Troubleshooting

- **Duplicate key errors on event writes** – ensure the initializer created the
	index or rerun `Initialize()`. Unique index collisions usually indicate a
	concurrency issue in the aggregate.
- **Read model updates never land** – confirm your read models implement
	`IMongoDbReadModel` and expose a writable `Version` property. Without it, the
	optimistic concurrency check fails silently.
- **Connection spikes on cold start** – reuse a singleton `MongoClient` instead
	of recreating it per request so the driver can manage pooling.
- **Changing collection names** – rename carefully and migrate existing data;
	EventFlow does not perform collection migrations automatically.

## See also

- [Event stores](event-stores.md#mongo-db)
- [Read model stores](read-stores.md#mongo-db)
- [Snapshots](../additional/snapshots.md)

