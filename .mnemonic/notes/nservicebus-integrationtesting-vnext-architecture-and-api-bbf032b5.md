---
title: NServiceBus.IntegrationTesting vNext architecture and API
tags:
  - architecture
  - vnext
  - grpc
  - docker
  - testcontainers
  - nservicebus
  - rabbitmq
  - postgresql
lifecycle: permanent
createdAt: '2026-03-12T19:20:03.779Z'
updatedAt: '2026-03-12T20:35:39.241Z'
project: https-github-com-mauroservienti-nservicebus-integrationtesting
projectName: NServiceBus.IntegrationTesting
memoryVersion: 1
---
# NServiceBus.IntegrationTesting vNext Architecture

## Existing Framework (src/)

- Endpoints run **in-process**, sharing `IntegrationScenarioContext` in memory
- Pipeline behaviors intercept: handler invocations, send/publish/reply, saga state
- Key limitation: all endpoints must share the same package versions

## vNext Spike (vNext/) — WORKING, all 6 tests pass in CI

Proves a new out-of-process architecture using gRPC + process isolation.

### Architecture

- **Test host**: gRPC server (Kestrel, dynamic port, HTTP/2 cleartext)
- **Agent**: gRPC client embedded in each endpoint; dials `NSBUS_TESTING_HOST` on startup
- **Communication**: bidirectional streaming RPC — agent streams events to host, host streams commands back (ExecuteScenario)
- **Containers**: RabbitMQ + PostgreSQL + endpoint containers share a Docker network

### Key Package Versions

- NServiceBus 10: **10.1.0** (net10.0); NServiceBus.RabbitMQ: **11.0.0**
- NServiceBus 9: **9.2.9** (net8.0); NServiceBus.RabbitMQ: **9.2.2**
- NServiceBus 8: **8.2.6** (net6.0+net472); NServiceBus.RabbitMQ: **8.0.1**
- NServiceBus.Persistence.Sql: **9.0.0** + Npgsql **10.0.1**
- Grpc.AspNetCore / Grpc.Net.Client / Grpc.Tools: 2.67.0
- Testcontainers / Testcontainers.RabbitMq / Testcontainers.PostgreSql: **4.10.0**

### NuGet TFM Alignment

- NSB 10 → **net10.0** | Agent.v10: net10.0
- NSB 9 → **net8.0** | Agent.v9: net8.0
- NSB 8 → **net6.0** + net472 | Agent.v8: net6.0 (`<LangVersion>12</LangVersion>` required)
- NServiceBus.RabbitMQ mirrors NSB: v11→net10.0, v9→net8.0, v8→net6.0+net472
- YetAnotherEndpoint (NSB 8 demo) targets **net8.0** because SampleMessages targets net8.0

### NServiceBus 10 Design Decisions

- `IWantToRunWhenEndpointStartsAndStops` NOT in NSB 10 core → agent connection is explicit
- Behaviors registered as instances: `configuration.Pipeline.Register(new Behavior(), "desc")`
- `config.Notifications()` does NOT exist in NSB 10; use `config.Recoverability().Failed(...)` instead

### Critical Config Gotchas

- **PostgreSQL + NSB SqlPersistence**: must call `dialect.JsonBParameterModifier(p => ...)` to set `NpgsqlDbType.Jsonb` on parameters — NSB avoids direct Npgsql dependency so it can't do this automatically. Without it, saga save fails at runtime.
- **Docker build context**: `vNext/.dockerignore` excludes `**/bin/` and `**/obj/` to prevent local arm64 artifacts from contaminating amd64 Docker builds and invalidating Testcontainers content-hash caching.
- **Testcontainers 4.10.0 BuildKit race condition**: On Linux Docker Engine (GitHub Actions), BuildKit tags images asynchronously. Fix: use `.WithName("localhost/nsb-integration-testing/{name}:latest")` in `ImageFromDockerfileBuilder` so CI can pre-build with the exact same tag. See `TestEnvironmentBuilder.cs` and the `Pre-build endpoint Docker images` CI step.

### Reporting Design

`HandlerInvokedEvent` and `MessageDispatchedEvent` have **no error fields**. Behaviors only report on success:

- `ReportingBehavior`: reports after `await next()` — if handler throws, no report sent
- `OutgoingReportingBehavior`: same
- Permanent failures reported via `MessageFailedEvent` only
- **Rationale**: tests observe business outcomes, not retry mechanics

### ObserveContext / ObserveResults API

`_env.Observe(correlationId, ct)` returns an `ObserveContext`. Fluent conditions:

- `.HandlerInvoked("Name")` — fires on first successful handler invocation
- `.SagaInvoked("Name")` — use `results.SagaInvoked("Name")`
- `.MessageDispatched("Name")` — fires on first outgoing message of that type
- Each has 3 overloads: no-arg, `Func<TEvent, bool>`, `Func<IReadOnlyList<TEvent>, bool>`
- `.WhenAllAsync()` — awaits all registered conditions, returns `ObserveResults`
- **Pattern**: register ALL conditions before awaiting `.WhenAllAsync()` to avoid missing fast events

### TestEnvironmentBuilder / TestEnvironment

```csharp
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(vNextDir)
    .UseRabbitMq()
    .UsePostgreSql()
    .AddEndpoint("Name", "relative/path/Dockerfile")
    .WithAgentConnectionTimeout(TimeSpan.FromSeconds(120))
    .StartAsync();
```

- `TestEnvironment` is `IAsyncDisposable`
- `.GetEndpoint(name)` → `EndpointHandle`; `.Observe(correlationId, ct)` → `ObserveContext`
- `.GetEndpointContainerLogsAsync(name)` → `(string Stdout, string Stderr)` for failure diagnostics
- `.WithExtraHost("host.docker.internal", "host-gateway")` applied to all endpoint containers

### Remaining Gaps

- Saga data and message payload access in predicates (Option A: JSON in proto event)
- MEL logging interception not implemented
