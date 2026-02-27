# NServiceBus.IntegrationTesting — Claude Context

## Project Purpose

Integration testing framework for NServiceBus endpoints. Allows running real endpoints
against real transports/persistence in tests.

- **`src/`** — existing framework: endpoints run in-process, sharing `IntegrationScenarioContext`
  in memory via `NServiceBus.AcceptanceTesting`. Key limitation: all endpoints must share the
  same package versions.
- **`vNext/`** — new out-of-process architecture using gRPC + Docker containers. All tests pass.

## vNext Architecture

- **Test host**: gRPC server (Kestrel, dynamic port, HTTP/2 cleartext) running in the test process
- **Agent**: gRPC client embedded in each endpoint; dials `NSBUS_TESTING_HOST` env var on startup
- **Communication**: bidirectional streaming RPC — agent streams events to host, host streams
  commands back (e.g. `ExecuteScenario`)
- **Containers**: RabbitMQ + PostgreSQL + endpoint containers share a Docker network via Testcontainers

## Key Package Versions

### NServiceBus 10 (SampleEndpoint, net10.0)

- NServiceBus: **10.1.0**; NServiceBus.RabbitMQ: **11.0.0**
- NServiceBus.Persistence.Sql: **9.0.0** + Npgsql **10.0.1**
- Agent: `NServiceBus.IntegrationTesting.Agent.v10` (net10.0)

### NServiceBus 9 (AnotherEndpoint, net9.0)

- NServiceBus: **9.2.9**; NServiceBus.RabbitMQ: **9.2.2**
- Agent: `NServiceBus.IntegrationTesting.Agent.v9` (net9.0)

### NSB version alignment rule

Each NSB major version number equals the target .NET version: NSB 8→net8.0, NSB 9→net9.0,
NSB 10→net10.0. NServiceBus.RabbitMQ matches NSB version for NSB 8 and 9; for NSB 10 it is
offset by one (NSB 10 → RabbitMQ 11).

### Shared infrastructure

- `SampleMessages`: **net8.0** (consumed by both net9.0 and net10.0 projects)
- Grpc.AspNetCore / Grpc.Net.Client / Grpc.Tools: **2.67.0**
- Testcontainers / Testcontainers.RabbitMq / Testcontainers.PostgreSql: **4.10.0**

## NServiceBus 10 Design Decisions

- `IWantToRunWhenEndpointStartsAndStops` is NOT in NSB 10 core (only in NSB.Host)
  → Agent connection is explicit: caller calls `await agent.ConnectAsync()` after `Endpoint.Start()`
- Behaviors registered as instances: `configuration.Pipeline.Register(new Behavior(), "desc")`
- `config.Notifications()` does NOT exist in NSB 10; use `config.Recoverability().Failed(...)` instead

## Critical Config Gotchas

- **PostgreSQL + NSB SqlPersistence**: must call `dialect.JsonBParameterModifier(p => ...)` to
  set `NpgsqlDbType.Jsonb` on parameters. Without it, saga save fails at runtime.
- **Docker build context**: `vNext/.dockerignore` excludes `**/bin/` and `**/obj/` to prevent
  local arm64 artifacts contaminating amd64 Docker builds.
- **Linux CI**: `WithExtraHost("host.docker.internal", "host-gateway")` is applied to all endpoint
  containers so `host.docker.internal` resolves on Linux Docker Engine. No-op on Docker Desktop.
- **Testcontainers 4.10.0 + BuildKit race**: On Linux Docker Engine, BuildKit tags images
  asynchronously. `BuildImageFromDockerfileAsync` returns before the tag is visible, so the
  immediate `InspectImageAsync` check fails with "has not been created". Fix: use
  `.WithName("localhost/nsb-integration-testing/{endpointName.ToLower()}:latest")` in
  `ImageFromDockerfileBuilder` (done in `TestEnvironmentBuilder`) and pre-build with the exact
  same tags in CI (`Pre-build endpoint Docker images` step). When the tag already exists,
  Docker completes the rebuild synchronously (full cache hit) and the check succeeds.
- **TimeoutRescheduleBehavior**: NSB 9/10 `RequestTimeout` uses `DelayDeliveryWith` (relative
  delay) internally. NSB 8 used `DoNotDeliverBefore`. Both cases must be handled; see
  `TimeoutRescheduleBehavior.cs`.
- **ARM64 / Apple Silicon + cross-compilation**: Do NOT use `--no-restore` on `dotnet publish`
  inside Dockerfiles. On Apple Silicon with Linux/AMD64 containers, the restore and publish
  must share the same RID context. Omitting `--no-restore` causes an implicit re-restore that
  is fast due to layer caching but avoids NETSDK1064 native asset mismatches.

## Reporting Design

`HandlerInvokedEvent` and `MessageDispatchedEvent` have **no error fields**. Behaviors report
on success only:

- `ReportingBehavior` (IInvokeHandlerContext): reports after `await next()` — handler throws → no report
- `OutgoingReportingBehavior`: same — reports after `await next()` succeeds only
- **Rationale**: tests observe business outcomes. Retried failures are transient noise.
- The retry policy may be configured shorter in `*.Testing` projects for test speed

## Production / Testing Separation Pattern

- `SampleEndpoint/` — zero testing dependencies
- `SampleEndpoint/SampleEndpointConfig.cs` — static `Create()` factory reused by testing wrapper
- `SampleEndpoint.Testing/` — wraps production config with `IntegrationTestingBootstrap`
- `SampleEndpoint.Testing/Dockerfile` — builds the testing image for containers
- `SampleEndpoint.Tests/` — test project; references `SampleEndpoint.Testing` with
  `ReferenceOutputAssembly="false"` (compile-time validation only, Docker image built at test time)

## Scenario Pattern

- `Scenario` abstract class in Agent library: `Name` + `Execute(IMessageSession, args, CT)`
- Registered via `IntegrationTestingBootstrap.RunAsync("Name", Config.Create, [new MyScenario()])`
- Test host calls `ExecuteScenarioAsync("EndpointName", "ScenarioName", args)` over gRPC
- Args are `Dictionary<string, string>` — no double-serialization; message created inside endpoint

## ObserveContext / ObserveResults API

`_env.Observe(correlationId, ct)` returns an `ObserveContext`. Fluent conditions:

- `.HandlerInvoked("Name")` — fires on first successful handler invocation
- `.SagaInvoked("Name")` — same but stored separately; use `results.SagaInvoked("Name")`
- `.MessageDispatched("Name")` — fires on first outgoing message of that type
- `.MessageFailed()` — fires when a message is permanently sent to the error queue;
  use `results.MessageFailed()` to get `EndpointName`, `MessageTypeName`, `ExceptionMessage`
- Each has 3 overloads: no-arg, `Func<TEvent, bool>`, `Func<IReadOnlyList<TEvent>, bool>`
- `.WhenAllAsync()` — awaits all conditions, returns `ObserveResults`
- **Pattern**: register ALL conditions before awaiting `.WhenAllAsync()` to avoid missing fast events

## TestEnvironmentBuilder / TestEnvironment

```csharp
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(vNextDir)
    .UseRabbitMq()          // optional; image override available
    .UsePostgreSql()        // optional; image override available
    .UseWireMock()          // optional; exposes _env.WireMock (WireMockServer)
    .AddEndpoint("Name", "relative/path/Dockerfile")
    .WithAgentConnectionTimeout(TimeSpan.FromSeconds(120))  // default
    .StartAsync();
```

`TestEnvironment` is `IAsyncDisposable`; owns network, infra containers, test host, endpoint containers.

## Saga Reporting

- `IsSaga`, `SagaIsNew`, `SagaIsCompleted`, `SagaNotFound`, `SagaId` fields on `HandlerInvokedEvent`
- Timeout detection: `OutgoingReportingBehavior` sets intent `"RequestTimeout"` when
  `Headers.IsSagaTimeoutMessage == bool.TrueString`
- Correlation ID propagated through full chain including saga timeouts via `AsyncLocal`

## Unit Tests (vNext/NServiceBus.IntegrationTesting.Tests/)

Fast unit tests — no Docker, no gRPC server required. Run with `dotnet test`.

- **ObserveResultsTests**: accessor methods (`HandlerInvocations`, `SagaInvocations`,
  `MessageDispatches` and their `.Last()` convenience variants), throws on missing key
- **ScanHelpersTests**: `ScanForHandlerEventsAsync`, `ScanForDispatchEventsAsync`,
  `ScanForFailureEventAsync` — filtering by correlation ID and type name, predicate
  satisfaction, cancellation, list predicate accumulation
- Scan helpers are `internal static` in `TestHostGrpcService`; exposed via
  `InternalsVisibleTo` in `Properties/AssemblyInfo.cs`

## Remaining Gaps

- **Saga data and message payload in events** (Option A: add `string message_json` and
  `string saga_data_json` to proto; serialize in behaviors; expose `T Message<T>()` and
  `T SagaData<T>()` helpers on `HandlerInvokedEvent` via `JsonSerializer.Deserialize<T>`)
- **MEL logging interception**: new `LogMessage` proto message; `ILoggerProvider` in Agent
  that reads `CurrentCorrelationId.Value` and streams log entries to the test host
- **Child-process endpoints**: `AddProcessEndpoint(name, exe, envVars?)` alongside
  `AddEndpoint` for endpoints that cannot be containerised; agent already works via
  `NSBUS_TESTING_HOST=http://localhost:{port}` (no agent changes needed)
- **Built-in transport/persistence support**: `TestEnvironmentBuilder` currently ships
  `UseRabbitMq()` and `UsePostgreSql()` convenience methods that spin up the matching
  Testcontainers module. Users can already bring their own external broker/DB without
  framework support, but there are no built-in `UseXxx()` helpers for:
  - Transports: AmazonSQS/SNS (LocalStack), Azure Service Bus, SQL Server Transport
  - Persistence: MySQL, SQL Server, MongoDB, DynamoDB (LocalStack), RavenDB
