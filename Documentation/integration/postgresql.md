---
layout: default
title: PostgreSQL
parent: Integration
nav_order: 4
---

# PostgreSQL

Use the `EventFlow.PostgreSql` integration when you want EventFlow to persist
events, snapshots, and read models in PostgreSQL. The package wraps the Npgsql
driver and DbUp migrations, giving you consistent configuration, retries, and
schema provisioning across the stack.

## Prerequisites

- A .NET application already wired with `EventFlow`.
- PostgreSQL 12 or later. The bundled scripts rely on `GENERATED ... AS IDENTITY`
  columns and user-defined types.
- Credentials that can execute `CREATE TABLE`, `CREATE TYPE`, and `CREATE INDEX`
  statements in the target database.
- Network access for every service that emits commands or processes read
  models.

## Install the NuGet package

Add the PostgreSQL integration to every project that configures EventFlow.

```bash
dotnet add package EventFlow.PostgreSql
```

## Configure EventFlow

Call `ConfigurePostgreSql` once to register the shared connection, migrator, and
transient retry strategy, then opt into the specific stores you need.

```csharp
public void ConfigureServices(IServiceCollection services)
{
  var postgres = PostgreSqlConfiguration.New
    .SetConnectionString(Configuration.GetConnectionString("eventflow-postgres"))
    .SetTransientRetryCount(3);

  services.AddEventFlow(o => o
    .ConfigurePostgreSql(postgres)
    .UsePostgreSqlEventStore()                               // Events
    .UsePostgreSqlSnapshotStore()                            // Snapshots (optional)
    .UsePostgreSqlReadModel<UserReadModel>()                 // Read models
    .UsePostgreSqlReadModel<UserNicknameReadModel, UserNicknameLocator>());
}
```

`ConfigurePostgreSql` wires up `IPostgreSqlConnection`, the DbUp-based
`IPostgreSqlDatabaseMigrator`, and the `PostgreSqlErrorRetryStrategy` used by
the event store and read models.

### Optional tuning

- Call `SetConnectionString("read-models", ...)` when you want read models to
  connect to a different database or replica.
- Adjust `SetTransientRetryCount` / `SetTransientRetryDelay` to tune retries
  for deadlocks (`SqlState 40P01`) and active-transaction conflicts (`SqlState 25001`).
- Increase `SetUpgradeExecutionTimeout` when migration batches take longer than
  five minutes.

## Event store

### Enable the PostgreSQL event store

Replace the in-memory default by calling `UsePostgreSqlEventStore()` after
`ConfigurePostgreSql`.

```csharp
services.AddEventFlow(o =>
  o.ConfigurePostgreSql(postgres)
   .UsePostgreSqlEventStore());
```

### Provision the schema

Run the embedded scripts once per environment to create the `EventFlow` table,
the `(AggregateId, AggregateSequenceNumber)` unique index, and the
`eventdatamodel_list_type` composite type used for batch inserts.

```csharp
await using var scope = services.BuildServiceProvider().CreateAsyncScope();
var migrator = scope.ServiceProvider.GetRequiredService<IPostgreSqlDatabaseMigrator>();
await EventFlowEventStoresPostgreSql.MigrateDatabaseAsync(migrator, cancellationToken);
```

The migrator is idempotent—rerunning it simply ensures the schema is present.
Lack of `CREATE TYPE` or `CREATE TABLE` permissions causes install-time failures.

### Operational notes

- `PostgreSqlEventPersistence` surfaces duplicate key violations (`SqlState 23505`)
  as `OptimisticConcurrencyException`; investigate aggregate concurrency if you
  see these at runtime.
- Event batches are appended inside a transaction. Monitor WAL growth and plan
  for appropriate autovacuum settings.
- The built-in retry strategy only retries deadlocks and active-transaction
  errors; unexpected exceptions bubble immediately.

## Snapshot store

Enable PostgreSQL snapshots with `.UsePostgreSqlSnapshotStore()` and run the
companion migration to create the `EventFlowSnapshots` table.

```csharp
services.AddEventFlow(o =>
  o.ConfigurePostgreSql(postgres)
   .UsePostgreSqlSnapshotStore());

await EventFlowSnapshotStoresPostgreSql.MigrateDatabaseAsync(migrator, cancellationToken);
```

Snapshots share a single table keyed by `(AggregateName, AggregateId)` and store
the serialized data plus metadata needed for upgrades. Duplicate writes are
ignored when a snapshot with the same sequence number already exists.

## Read model store

### Register the store

`UsePostgreSqlReadModel<T>` (or the locator overload) plugs the SQL read-store
implementation into EventFlow.

```csharp
services.AddEventFlow(o =>
  o.ConfigurePostgreSql(postgres)
   .UsePostgreSqlReadModel<UserReadModel>()
   .UsePostgreSqlReadModel<UserNicknameReadModel, UserNicknameLocator>());
```

### Implement the read model

PostgreSQL read models should implement `IReadModel` and either derive from
`PostgreSqlReadModel` or decorate key properties with the provided attributes.

```csharp
[Table("ReadModel-User")]
public class UserReadModel : PostgreSqlReadModel,
    IAmReadModelFor<UserAggregate, UserId, UserRegistered>
{
  public string DisplayName { get; set; } = default!;

  public Task ApplyAsync(
    IReadModelContext context,
    IDomainEvent<UserAggregate, UserId, UserRegistered> @event,
    CancellationToken cancellationToken)
  {
    AggregateId = @event.AggregateIdentity.Value;
    DisplayName = @event.AggregateEvent.DisplayName;
    UpdatedTime = DateTimeOffset.UtcNow;
    if (CreateTime == default)
    {
      CreateTime = UpdatedTime;
    }
    return Task.CompletedTask;
  }
}
```

The base class marks `AggregateId` with `[PostgreSqlReadModelIdentityColumn]` and
`LastAggregateSequenceNumber` with `[PostgreSqlReadModelVersionColumn]`. Use
`[PostgreSqlReadModelIgnoreColumn]` to skip properties that are not persisted.

### Create the table

EventFlow does not auto-create read model tables. Deploy DDL that matches your
read model shape—by convention the table name is `ReadModel-[TypeName]`.

```sql
CREATE TABLE IF NOT EXISTS "ReadModel-User" (
    Id BIGINT GENERATED BY DEFAULT AS IDENTITY,
    AggregateId VARCHAR(64) NOT NULL,
    CreateTime TIMESTAMPTZ NOT NULL,
    UpdatedTime TIMESTAMPTZ NOT NULL,
    LastAggregateSequenceNumber INT NOT NULL,
    DisplayName TEXT NOT NULL,
    CONSTRAINT "PK_ReadModel-User" PRIMARY KEY (Id)
);

CREATE INDEX IF NOT EXISTS "IX_ReadModel-User_AggregateId"
    ON "ReadModel-User" (AggregateId);
```

At a minimum, keep the identity column, the optimistic concurrency column, and
the fields mined by your query handlers. Add additional indexes to match your
query patterns.

### Run read model migrations

Package the DDL alongside your application and execute it with the shared
`IPostgreSqlDatabaseMigrator`.

```csharp
var migrator = scope.ServiceProvider.GetRequiredService<IPostgreSqlDatabaseMigrator>();
await migrator.MigrateDatabaseUsingEmbeddedScriptsAsync(
  typeof(Program).Assembly,
  scriptNamespace: "MyCompany.MyApp.SqlScripts",
  cancellationToken);
```

The tests in `Source/EventFlow.PostgreSql.Tests` demonstrate this pattern: embed
versioned SQL files and invoke the migrator during startup or deployment.

## Local development quickstart

Run a disposable PostgreSQL container and point `ConfigurePostgreSql` to it.

```bash
docker run --rm -p 5432:5432 --name eventflow-postgres \
  -e POSTGRES_PASSWORD=eventflow \
  -e POSTGRES_DB=eventflow \
  postgres:16
```

## Troubleshooting

- **`SqlState 23505` (duplicate key)** – the unique index on
  `(AggregateId, AggregateSequenceNumber)` rejected a reinsert. Inspect aggregate
  concurrency or idempotency guards.
- **`eventdatamodel_list_type` does not exist** – rerun
  `EventFlowEventStoresPostgreSql.MigrateDatabaseAsync`; the composite type is
  required for batch inserts.
- **Missing read model rows** – confirm the table exists, the identity column is
  marked with `[PostgreSqlReadModelIdentityColumn]`, and the process has write
  access; otherwise updates are ignored.
- **Permission errors during migration** – grant `CREATE TABLE`, `CREATE TYPE`,
  and `CREATE INDEX` to the login executing the migrator.

## See also

- [Event stores](event-stores.md#postgresql-event-store)
- [Read model stores](read-stores.md)
- [Snapshots](../additional/snapshots.md)

