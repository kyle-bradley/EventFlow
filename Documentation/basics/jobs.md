---
layout: default
title: Jobs
parent: Basics
nav_order: 2
---

# Jobs

Jobs let you execute work outside of the current request or process. They are
ideal when something should happen later, needs retries, or has to run on a
different machine. EventFlow ships with the primitives required to define,
register, and schedule jobs.

Typical use cases include:

- Publishing a command at a specific time in the future
- Retrying transient operations without blocking the caller
- Deferring background work to a dedicated processor

```csharp
var jobScheduler = resolver.Resolve<IJobScheduler>();
var job = PublishCommandJob.Create(new SendEmailCommand(id), resolver);

await jobScheduler.ScheduleAsync(
    job,
    TimeSpan.FromDays(7),
    CancellationToken.None);
```

The code above schedules the `SendEmailCommand` to run seven days from now.

!!! warning
    The default `IJobScheduler` implementation in EventFlow is the
    `InstantJobScheduler`. It executes jobs **immediately in the current
    process**, ignoring `runAt` and `delay` arguments. To perform actual delayed
    or distributed execution you must register another scheduler, for example
    the Hangfire integration shown later on this page.

!!! note
    Jobs must serialize to JSON cleanly, because schedulers typically persist
    the job payload. Review the guidance on [value
    objects](../additional/value-objects.md) and ensure any commands emitted via
    `PublishCommandJob` serialize correctly as well.

## Create your own jobs

Implement the `IJob` interface for each job type you want to schedule and
register it with EventFlow.

Here's an example of a job implementing `IJob`

```csharp
[JobVersion("LogMessage", 1)]
public class LogMessageJob : IJob
{
  public LogMessageJob(string message)
  {
    Message = message;
  }

  public string Message { get; }

  public Task ExecuteAsync(
    IServiceProvider serviceProvider,
    CancellationToken cancellationToken)
  {
    var log = serviceProvider.GetRequiredService<ILogger<LogMessageJob>>();
    log.LogDebug(Message);
    return Task.CompletedTask;
  }
}
```

The `JobVersion` attribute sets the logical job name and version used during
serialization. This allows you to move or rename the CLR type without breaking
existing scheduled jobs. If you omit the attribute, EventFlow falls back to the
type name and version `1`.

Here's how the job is registered in EventFlow.

```csharp
public void ConfigureServices(IServiceCollection services)
{
  services.AddEventFlow(ef =>
  {
    ef.AddJobs(typeof(LogMessageJob));
  });
}
```

Then schedule the job through `IJobScheduler`:

```csharp
var jobScheduler = serviceProvider.GetRequiredService<IJobScheduler>();
var job = new LogMessageJob("Great log message");
await jobScheduler.ScheduleAsync(
  job,
  TimeSpan.FromDays(7),
  CancellationToken.None);
```

## Hangfire

For production-grade scheduling scenarios we recommend
[Hangfire](http://hangfire.io/). Install the `EventFlow.Hangfire` package and
configure EventFlow to use the Hangfire-backed scheduler. Hangfire supports
multiple storage providers (SQL Server, PostgreSQL, MongoDB, Redis, etc.). Use
the in-memory storage only during development.

```csharp
private void RegisterHangfire(IEventFlowOptions eventFlowOptions)
{
  eventFlowOptions.ServiceCollection
    .AddHangfire(configuration => configuration.UseSqlServerStorage(connectionString))
    .AddHangfireServer();

  eventFlowOptions.UseHangfireJobScheduler();
}
```

!!! note
  `UseHangfireJobScheduler()` simply swaps the scheduler implementation in
  EventFlow. You are still responsible for configuring Hangfire storage,
  servers, and dashboards according to your environment.
