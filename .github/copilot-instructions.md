# Copilot Instructions for EventFlow

These guidelines govern contributions within the EventFlow code base hosted at https://github.com/eventflow/EventFlow/. Follow them whenever collaborating in this repository to stay aligned with the project’s expectations.

## Architecture snapshot
- EventFlow is a CQRS+ES framework; the core runtime lives in `Source/EventFlow` and exposes aggregates, commands, queries, read stores, sagas, jobs, and snapshots.
- Command flow: clients call `CommandBus` (`Source/EventFlow/CommandBus.cs`) which resolves handlers, invokes aggregates deriving from `AggregateRoot<TAggregate, TIdentity>`, and emits events that pipe through subscribers and read-store dispatchers.
- Aggregates load and persist via `IAggregateStore`/`IEventStore`; defaults use the in-memory persistence registered in `EventFlowOptions`, while integration packages under `Source/EventFlow.*` swap in specific stores.
- Read models implement `IReadModel` plus `IAmReadModelFor<...>`; dispatch logic sits in `ReadStores` and uses metadata to map events to view updates.
- Sagas and jobs live under `Source/EventFlow/Sagas` and `Source/EventFlow/Jobs`, coordinating cross-aggregate workflows and deferred execution.
- Documentation that explains the concepts is checked in under `Documentation/`; updates should travel with code changes.

## Extension & configuration guide
- Dependency injection starts with `services.AddEventFlow(o => { ... })` (`Source/EventFlow/Extensions/ServiceCollectionExtensions.cs`); chain option methods to register events, commands, read models, snapshots, sagas, and custom services.
- Use the fluent helpers in `EventFlowOptions` (`Source/EventFlow/EventFlowOptions.cs`) such as `.AddEvents`, `.AddCommands`, `.UseInMemoryReadStoreFor<TReadModel>()`, `.ConfigureOptimisticConcurrencyRetry(...)`, or `.UseEventPersistence<T>()` to pivot storage/backends.
- Strongly typed IDs must derive from `Identity<T>` (`Source/EventFlow/Core/Identity.cs`); create new IDs via `ExampleId.New` or `Identity<T>.With(Guid)` to honor prefix validation.
- When adding domain objects, follow the naming pattern `ThingyAggregate` + `ThingyId` + `ThingyEvent`; see `EventFlow.TestHelpers/Aggregates/Thingy*` for canonical examples including event emit/apply patterns.
- Integration modules (MongoDB, MsSql, PostgreSql, Redis, SQLite, etc.) expose option extensions in their `Extensions/` folder; replicate those patterns when introducing new infrastructure.
- Prefer using `EventFlow.TestHelpers` base classes and fixtures when authoring tests so categories, logging, and deterministic IDs behave consistently.

## Build, test, and verification
- The solution is organized under `EventFlow.sln`; build with `dotnet build EventFlow.sln` (warnings are treated as errors via `Source/Directory.Build.props`).
- Unit tests target `netcoreapp3.1`, `net6.0`, and `net8.0`; run fast feedback with `dotnet test EventFlow.sln --filter "Category!=integration"` and rely on `EventFlow.TestHelpers.Categories` constants when tagging new suites.
- Integration tests span external services (MongoDB, PostgreSQL, SQL Server, RabbitMQ, Elasticsearch, EventStore); start containers with `docker-compose up` before executing the corresponding `*.Tests` projects or include the `integration` category filter.
- Source generators and analyzers live in `Source/EventFlow.SourceGenerators` and `Source/EventFlow.CodeStyle`; ensure the .NET SDK version supports C# 12 and keep analyzer warnings clean.
- Documentation builds use MkDocs (`requirements.txt`); run `pip install -r requirements.txt` followed by `mkdocs serve` when verifying doc updates.

## Coding conventions & review tips
- Favor async APIs and accept `CancellationToken` parameters throughout—the core dispatchers expect cooperative cancellation (see `CommandBus.PublishAsync` and `AggregateStore` methods).
- New events should inherit `AggregateEvent<TAggregate, TIdentity>`, carry immutable data, and rely on aggregate `Apply` methods to mutate state; never mutate state directly inside command handlers.
- Subscribers and read stores should request dependencies via constructor injection and avoid static singletons; look at `Source/EventFlow/Subscribers` for the expected interface contracts.
- When wiring new persistence, register required DI services before calling `.UseEventPersistence<T>()` to avoid the `RemoveAll<IEventPersistence>()` guard removing your registration.
- Keep public APIs binary compatible where possible; breaking changes require updates in `Documentation/` and `RELEASE_NOTES.md`.
- Mirror existing namespace layout (`EventFlow.{Feature}`) and group files into folders matching their conceptual role to keep source generators and discovery heuristics effective.

## Operational safeguards
- Avoid invoking GitHub management tools that mutate remote state (issues, pull requests, repositories, projects, workflows, labels, security alerts, notifications, etc.) unless the user has granted explicit permission in the current conversation.
- Never run mutating `git` commands (commit, push, merge, rebase, reset, clean, etc.) without explicit user authorization; limit `git` usage to read-only inspection by default.
- If permission is unclear, pause and ask the user before attempting any action that could alter repository or GitHub state.
