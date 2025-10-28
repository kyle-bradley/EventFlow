---
layout: default
title: RabbitMQ
parent: Integration
nav_order: 2
---

# RabbitMQ

EventFlow ships with a RabbitMQ integration that fans every persisted domain event out to an exchange. This is
useful when downstream systems (read models, legacy services, analytics pipelines, and so on) must react to
aggregate changes without being tightly coupled to the write model.

The integration focuses on **publishing**. It does not create queues or start consumers for you—topology remains
an infrastructure concern so you can keep the messaging contract explicit.

## Prerequisites

- RabbitMQ 3.8 or newer (older versions work, but automatic recovery and federation are more reliable on ≥3.8).
- The [`EventFlow.RabbitMQ`](https://www.nuget.org/packages/EventFlow.RabbitMQ) package alongside the core EventFlow packages.
- A pre-provisioned exchange (typically a durable topic exchange) plus the queues/bindings you want to consume from.
  EventFlow does **not** declare exchanges or queues automatically.

## Install and register the publisher

```bash
dotnet add package EventFlow.RabbitMQ
```

Enable the publisher when you build your `EventFlowOptions`.

```csharp
using EventFlow.RabbitMQ;
using EventFlow.RabbitMQ.Extensions;

var rabbitUri = new Uri("amqp://guest:guest@localhost:5672/");

services.AddEventFlow(options => options
    // ... register aggregates, commands, read models, etc.
    .PublishToRabbitMq(
        RabbitMqConfiguration.With(
            rabbitUri,
            persistent: true,            // mark messages as durable
            modelsPrConnection: 5,       // pooled channels per connection
            exchange: "eventflow")));   // topic exchange to publish to
```

`RabbitMqConfiguration.With` exposes the following knobs:

- `uri` – The AMQP URI, including credentials and vhost. Use `amqps://` when TLS is required.
- `persistent` – Whether RabbitMQ should persist messages to disk (`true` by default). Set this to `false` for
  transient data.
- `modelsPrConnection` – How many channels (models) the integration pools per connection. Increase the value if you
  have a high write rate and observe channel contention.
- `exchange` – Name of the exchange EventFlow publishes to. The exchange must already exist.

Once configured, EventFlow registers an `ISubscribeSynchronousToAll` subscriber that ships each domain event to
RabbitMQ right after the event is committed to the event store. The command is considered complete only after the
publish succeeds (or ultimately fails), so RabbitMQ errors surface to the caller.

## Exchange and routing conventions

By default messages are published with:

- **Exchange** – The value supplied via `RabbitMqConfiguration.With` (defaults to `eventflow`).
- **Routing key** – `eventflow.domainevent.{aggregate-name}.{event-name}.{event-version}` where each segment is
  slugified (lowercase, dashes for PascalCase).

For example, an event named `UserRegistered` version `1` from `CustomerAggregate` produces:

```
eventflow.domainevent.customer.user-registered.1
```

### Creating queues and bindings

EventFlow does not create queues. Bind your own queues to the configured exchange using the routing keys that matter
to a consumer. With the default topic exchange, you can subscribe to an entire aggregate or event family:

- `eventflow.domainevent.customer.*` – All events from `CustomerAggregate`.
- `eventflow.domainevent.*.user-registered.*` – Any `UserRegistered` event regardless of aggregate.

```csharp
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.ExchangeDeclare("eventflow", ExchangeType.Topic, durable: true);
channel.QueueDeclare("customer-updates", durable: true, exclusive: false, autoDelete: false);
channel.QueueBind("customer-updates", "eventflow", "eventflow.domainevent.customer.#");
```

Run similar provisioning code (or infrastructure as code) before your service starts or during deployment.

## Message payload and headers

The integration serializes the aggregate event using EventFlow’s regular JSON serializer. Metadata is sent alongside
the message in two places:

- **Body** – JSON payload with the actual event data. This is identical to what the event store persists.
- **Headers** – A `Dictionary<string,string>` containing EventFlow metadata such as:
  - `event_name`, `event_version`
  - `aggregate_id`, `aggregate_name`, `aggregate_sequence_number`
  - `event_id`, `batch_id`, `timestamp`, `timestamp_epoch`
  - `source_id` when available

Example body:

```json
{
  "UserId": "dcd3f2e1-6f9b-4fcb-8901-9a5f6f2f4c0a",
  "Email": "customer@example.com",
  "RegisteredAt": "2025-09-20T17:53:41.197842Z"
}
```

Example headers:

| Header | Example value |
| --- | --- |
| `event_name` | `user-registered` |
| `event_version` | `1` |
| `aggregate_name` | `customer` |
| `aggregate_id` | `customer-5b0d9af0` |
| `aggregate_sequence_number` | `42` |
| `event_id` | `01JF2ZNKX1QZS5CJ1V6AQ13RPT` |
| `timestamp` | `2025-09-20T17:53:41.2012129Z` |

Leverage these headers to enrich logs, implement idempotency, or drive filtering logic in consumers.

## Reliability characteristics

- **Persistent messages** – Enabled by default via `basicProperties.Persistent = true` when configured.
- **Connection pooling** – A connection is opened per URI and keeps a pool of AMQP channels (models) to avoid throttling.
  Tune `modelsPrConnection` for your throughput profile.
- **Automatic recovery** – The RabbitMQ client enables topology and automatic connection recovery so brief network blips
  self-heal.
- **Retry strategy** – Transient `BrokerUnreachableException`, `OperationInterruptedException`, and `EndOfStreamException`
  are retried up to three times with a 25 ms backoff. Replace `IRabbitMqRetryStrategy` in the container if you need custom
  retry logic.

Failures that propagate after retries cause the current command to fail; the publish will be retried the next time the
command is executed or when the aggregate emits subsequent events.

## Customizing the integration

- **Alternate exchange or routing key** – Replace the registered `IRabbitMqMessageFactory` with your own implementation
  to target different exchanges, enrich headers, or transform the payload.
- **Custom publish mechanics** – Override `IRabbitMqPublisher` if you need publisher confirms, tracing, or batch semantics.
- **Asynchronous publishing** – If you prefer to publish outside the command execution pipeline, register your own
  `ISubscribeAsynchronousToAll` implementation and publish from there instead of relying on the built-in synchronous publisher.

```csharp
services.TryAddSingleton<IRabbitMqMessageFactory, CustomRabbitMqMessageFactory>();
```

## Troubleshooting

- `NOT_FOUND - no exchange` – The exchange name does not exist. Create it manually or update the configuration.
- `NO_ROUTE` warnings – Nothing is bound to the routing key. Check your queue bindings.
- **Channel busy or blocked** – Increase `modelsPrConnection` or scale out publishers.
- **Silent drops** – Inspect consumer acknowledgements and dead-letter queues; EventFlow only publishes and cannot observe
  downstream failures.

For general guidance on subscribers and out-of-order delivery considerations, review the
[subscribers documentation](../basics/subscribers.md).
