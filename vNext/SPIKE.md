# vNext Spike — Out-of-Process Integration Testing with gRPC

## Goal

Prove a new architecture for `NServiceBus.IntegrationTesting` that eliminates the key
limitation of the existing framework: all endpoints must share the same NServiceBus package
version because they run in-process together.

The new architecture runs each endpoint in its own Docker container. The test process is
the gRPC server; each endpoint embeds a gRPC client agent that dials home on startup.

**Status: spike active.** Multi-endpoint interaction, outgoing message tracking, failure
tracking, saga state reporting, and saga timeout flows are all proven end-to-end.

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Test process (SampleEndpoint.Tests)                    │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │  TestHostServer (ASP.NET Core / Kestrel)         │  │
│  │  - Binds to 0.0.0.0 on a dynamic port            │  │
│  │  - TestHostGrpcService handles connections       │  │
│  └──────────────────────────────────────────────────┘  │
└────────────────────────┬────────────────────────────────┘
         gRPC (bidirectional streaming)
         host.docker.internal:{port}
                         │
         ┌───────────────┼────────────────┐
         │               │                │
┌────────▼──────┐ ┌──────▼──────┐  [more endpoints...]
│  SampleEndpt  │ │ AnotherEndpt│
│  container    │ │  container  │
│               │ │             │
│  Agent ◄──────┤ │  Agent ◄────┤  (gRPC client, dials test host)
│  NSB endpoint │ │ NSB endpoint│
└───────────────┘ └─────────────┘
         │               │
         └───────┬───────┘
           RabbitMQ container        PostgreSQL container
           (alias "rabbitmq")        (alias "postgres" — used by SampleEndpoint saga)
```

**Agent → Host messages:** `ConnectMessage`, `HandlerInvokedMessage`,
`MessageDispatchedMessage`, `MessageFailedMessage`

**Host → Agent messages:** `ReadyMessage`, `ExecuteScenarioMessage`

The proto definition lives in `proto/testing.proto`.

---

## Project Structure

```
vNext/
├── proto/
│   └── testing.proto                  # gRPC service + message definitions
│
├── NServiceBus.IntegrationTesting.Agent/
│   ├── AgentService.cs                # gRPC client; connects, reports, dispatches scenarios
│   ├── IntegrationTestingBootstrap.cs # Entry point helper for Testing projects
│   ├── IncomingCorrelationIdBehavior.cs # Reads CorrelationIdHeader → AsyncLocal
│   ├── OutgoingCorrelationIdBehavior.cs # Stamps AsyncLocal value → CorrelationIdHeader
│   ├── ReportingBehavior.cs           # Reports handler invocations (+ saga state)
│   ├── OutgoingReportingBehavior.cs   # Reports dispatched messages (intent, errors)
│   ├── SagaInfo.cs                    # Snapshot of saga state at invocation time
│   └── Scenario.cs                    # Abstract base class for user-defined scenarios
│
├── NServiceBus.IntegrationTesting.Containers/
│   ├── TestHostServer.cs              # Starts Kestrel gRPC server; exposes Address/ContainerAddress
│   └── TestHostGrpcService.cs         # Server-side: WaitForAgentAsync, ExecuteScenarioAsync,
│                                      #   WaitForHandlerInvocationAsync,
│                                      #   WaitForMessageDispatchedAsync,
│                                      #   WaitForMessageFailureAsync
│
├── SampleMessages/                    # Shared message contracts (IMessage)
│   ├── SomeMessage.cs
│   ├── AnotherMessage.cs
│   └── SomeReply.cs
│
├── SampleEndpoint/                    # Production endpoint — zero testing dependencies
│   ├── SampleEndpointConfig.cs        # Static Create() factory (reused by Testing project)
│   ├── Program.cs
│   └── Handlers/
│       ├── SomeMessageHandler.cs      # Handles SomeMessage → sends AnotherMessage
│       ├── SomeReplyHandler.cs        # Handles SomeReply (reply from AnotherEndpoint)
│       ├── SomeReplySaga.cs           # Starts on SomeReply; sets 20s timeout
│       ├── SomeReplySagaData.cs       # ContainSagaData with SomeMessageCorrelationId
│       ├── SomeReplySagaTimeout.cs    # Internal timeout marker
│       ├── SagaCompletedMessage.cs    # Sent when timeout fires (IMessage, internal)
│       └── SagaCompletedMessageHandler.cs  # Test done condition handler
│
├── SampleEndpoint.Testing/            # Testing wrapper for SampleEndpoint
│   ├── Program.cs                     # Calls IntegrationTestingBootstrap.RunAsync with scenarios
│   ├── SomeMessageScenario.cs         # Named scenario: sends SomeMessage via real IMessageSession
│   ├── SampleEndpoint.Testing.csproj  # Refs SampleEndpoint + Agent
│   └── Dockerfile                     # Build context: vNext/
│
├── AnotherEndpoint/                   # Production endpoint — zero testing dependencies
│   ├── AnotherEndpointConfig.cs
│   ├── Program.cs
│   └── Handlers/
│       └── AnotherMessageHandler.cs   # Handles AnotherMessage → replies with SomeReply
│
├── AnotherEndpoint.Testing/           # Testing wrapper for AnotherEndpoint
│   ├── Program.cs                     # No scenarios — AnotherEndpoint is passive in tests
│   ├── AnotherEndpoint.Testing.csproj
│   └── Dockerfile
│
└── SampleEndpoint.Tests/              # NUnit test project
    ├── WhenSomeMessageIsSent.cs        # Full multi-endpoint test (inc. saga timeout test)
    └── SampleEndpoint.Tests.csproj    # Refs Containers, SampleMessages; builds Testing
                                       #   projects as ReferenceOutputAssembly=false
```

---

## Key Design Decisions

### 1. Production endpoints have zero testing dependencies

**Problem:** The v1 framework requires `EnableIntegrationTestingAgent()` in the production
`Program.cs`, creating a coupling between production code and the testing assembly.

**Solution:** A separate `*.Testing` companion project per endpoint:
- `SampleEndpoint/` — no reference to the Agent library
- `SampleEndpoint.Testing/` — references both `SampleEndpoint` and `NServiceBus.IntegrationTesting.Agent`

The endpoint config is extracted to a static `SampleEndpointConfig.Create()` factory so
both `Program.cs` (production) and `SampleEndpoint.Testing/Program.cs` (test mode) can
reuse it without duplication.

The test project references `SampleEndpoint.Testing` with `ReferenceOutputAssembly=false`
and `Private=false` — this triggers a compile-time build for validation without pulling the
assembly into the test process. The Docker image is built at test runtime via
`ImageFromDockerfileBuilder`.

### 2. Scenario pattern (replaces SendKickOffAsync)

**Problem:** The old `SendKickOffAsync<T>` approach serialized the message payload in the
test process (System.Text.Json with default options), transmitted it as a proto string, then
deserialized it again in the agent process, then let NServiceBus re-serialize it for the
transport. If the endpoint uses different serializer options, values can be silently
corrupted.

**Solution:** Named `Scenario` classes defined in the `*.Testing` project:
```csharp
public class SomeMessageScenario : Scenario
{
    public override string Name => "SomeMessage";
    public override async Task Execute(IMessageSession session,
        IDictionary<string, string> args, CancellationToken ct)
        => await session.Send(new SomeMessage { Id = Guid.Parse(args["ID"]) });
}
```

Scenarios run **inside the endpoint process** using the real, fully-configured
`IMessageSession`. No cross-process serialization of message payloads occurs.

The test just passes name + args:
```csharp
var correlationId = await _testHost.GrpcService.ExecuteScenarioAsync(
    "SampleEndpoint", "SomeMessage", new Dictionary<string, string> { ["ID"] = Guid.NewGuid().ToString() });
```

The returned `correlationId` is a server-assigned GUID that flows through all message
headers via `CorrelationIdHeader`, allowing the test to filter events by their originating
scenario invocation — making concurrent test execution safe.

### 3. Correlation ID propagation

Every message in a chain carries the correlation ID assigned by the test host when
`ExecuteScenarioAsync` is called. The pipeline is:

1. `AgentService.ExecuteScenarioAsync` sets `CurrentCorrelationId.Value` (AsyncLocal)
2. `OutgoingCorrelationIdBehavior` stamps `CorrelationIdHeader` onto every outgoing message
3. `IncomingCorrelationIdBehavior` reads `CorrelationIdHeader` from each incoming message
   and restores `CurrentCorrelationId.Value` in the async context
4. All reporting behaviors read `CurrentCorrelationId.Value` to tag events

This propagates through the full chain: handlers → sends → saga timeouts → timeout handlers.
Tests filter all events by `correlationId`, making concurrent tests safe.

### 4. Outgoing message and failure reporting

Two additional behaviors are registered alongside `ReportingBehavior`:

- **`OutgoingReportingBehavior`** (`IOutgoingLogicalMessageContext`): fires for every
  dispatched message when a correlation ID is active. Detects saga timeout requests via
  `Headers.IsSagaTimeoutMessage` and reports them with intent `"RequestTimeout"`. Reports
  in `finally` so dispatch errors are also captured.

- **Failure hook** (`config.Recoverability().Failed(...).OnMessageSentToErrorQueue(...)`):
  reports messages that exhaust retries, tagged with their correlation ID.

### 5. Saga state reporting

`ReportingBehavior` captures saga state from `context.Extensions.TryGet<ActiveSagaInstance>()`
and includes it in `HandlerInvokedMessage`: `IsSaga`, `SagaIsNew`, `SagaIsCompleted`,
`SagaId`, `SagaTypeName`, `SagaNotFound`. This gives tests full visibility into the saga
lifecycle without any saga-specific test API.

### 6. NServiceBus 10 constraints

- **No `IWantToRunWhenEndpointStartsAndStops`** in NSB 10 core (moved to NSB.Host).
  Agent connection is therefore explicit: `IntegrationTestingBootstrap` calls
  `agentService.ConnectAsync(endpointInstance)` after `Endpoint.Start()`.

- **No feature auto-discovery** — NSB 10 is moving away from assembly scanning toward
  trimming support. Defining a test agent as an NSB Feature that auto-registers isn't viable.
  Explicit opt-in via the `*.Testing` project pattern is the right approach.

- **Behaviors registered as instances**, not via DI:
  `configuration.Pipeline.Register(new ReportingBehavior(agentService), "description")`.

- **Failure hook**: `config.Recoverability().Failed(c => c.OnMessageSentToErrorQueue(...))`.
  `config.Notifications()` does not exist in NSB 10.

- **Concurrent gRPC writes**: `RequestStream.WriteAsync` must not be called concurrently.
  `AgentService` serializes all writes via a `SemaphoreSlim _writeLock`.

### 7. Container networking

- `TestHostServer` binds Kestrel to `IPAddress.Any` (not just `localhost`) so containers
  can reach it.
- The test passes `NSBUS_TESTING_HOST = http://host.docker.internal:{port}` to containers.
  `host.docker.internal` is resolved by Docker Desktop on macOS and Windows.
- RabbitMQ and PostgreSQL are reached by containers via Docker network aliases
  (`rabbitmq`, `postgres`) at their default ports — no host port mapping needed for
  inter-container traffic.
- `TestHostServer.ContainerAddress` returns `http://host.docker.internal:{port}`;
  `TestHostServer.Address` returns `http://localhost:{port}` for in-process use.

### 8. Dockerfile: no `--no-restore` on publish

On Apple Silicon (ARM64) with Docker Desktop running Linux/AMD64 containers, using
`dotnet publish --no-restore` after a `dotnet restore` layer causes a native asset mismatch
(`NETSDK1064`). The restore and publish steps must use the same runtime, which they do when
`--no-restore` is omitted. The implicit re-restore is fast due to layer caching.

---

## Proven End-to-End Flow

```
Test                  SampleEndpoint                    AnotherEndpoint
 │                         │                                   │
 │── ExecuteScenario ──────►│                                   │
 │   (SomeMessage)          │                                   │
 │                [SomeMessageScenario.Execute]                  │
 │                session.Send(SomeMessage)                      │
 │                          │                                   │
 │                SomeMessageHandler                             │
 │◄── HandlerInvoked ───────│                                   │
 │                          │────── AnotherMessage ────────────►│
 │                          │                          AnotherMessageHandler
 │◄── HandlerInvoked ───────┼───────────────────────────────────│
 │                          │◄────── Reply(SomeReply) ──────────│
 │                SomeReplyHandler + SomeReplySaga (both start)  │
 │◄── HandlerInvoked (SomeReplyHandler) ────────────────────────│
 │◄── HandlerInvoked (SomeReplySaga, IsSaga=true, IsNew=true) ──│
 │                          │                                   │
 │             [20 seconds elapse — saga timeout fires]          │
 │                          │                                   │
 │               SomeReplySaga.Timeout                           │
 │◄── HandlerInvoked (SomeReplySaga, IsCompleted=true) ─────────│
 │               context.SendLocal(SagaCompletedMessage)         │
 │                          │                                   │
 │               SagaCompletedMessageHandler                     │
 │◄── HandlerInvoked (SagaCompletedMessageHandler) ─────────────│
 │                          │                                   │
 │ Assert saga assertions   │                                   │
```

---

## Package Versions (as of spike)

| Package                           | Version  | Notes                                      |
|-----------------------------------|----------|--------------------------------------------|
| NServiceBus                       | 10.1.0   | targets net10.0                            |
| NServiceBus.RabbitMQ              | 11.0.0   | NSB 10.x / RabbitMQ transport 11.x        |
| NServiceBus.Persistence.Sql       | 9.0.0    | NSB 10.x / Persistence.Sql 9.x            |
| Npgsql                            | 10.0.1   | `JsonBParameterModifier` required (see §6) |
| Grpc.AspNetCore / Grpc.Net.Client | 2.67.0   |                                            |
| Google.Protobuf                   | 3.29.3   |                                            |
| Testcontainers                    | 4.10.0   | 4.1.0 breaks with containerd store         |
| Testcontainers.RabbitMq           | 4.10.0   |                                            |
| Testcontainers.PostgreSql         | 4.10.0   |                                            |

---

## Known Gaps / Next Steps

### MEL logging interception (medium)

Register a custom `ILoggerProvider` in `IntegrationTestingBootstrap` that captures
`Microsoft.Extensions.Logging` entries from handler code and streams them over gRPC to the
test host.

**Approach:**

1. New proto message `LogMessage` (`endpoint_name`, `level`, `category`, `message`,
   `exception`, `correlation_id`) added to `AgentToHostMessage` oneof.
2. `IntegrationTestingLoggerProvider : ILoggerProvider` + inner `ILogger` implementation
   in the Agent library. Reads `AgentService.CurrentCorrelationId.Value` at log time —
   since MEL logging typically fires in the same async context as the handler, the value
   is available for logs within the NSB pipeline.
3. Registered in `IntegrationTestingBootstrap` via `LoggerFactory.Create(b => b.AddProvider(...))`.
   The factory is wired into the NSB service collection so handler `ILogger<T>` dependencies
   resolve the capturing factory.
4. `TestHostGrpcService` exposes `WaitForLogAsync(correlationId, level, ...)` using the
   same fan-out channel pattern as handler/dispatch events.

**Design choices to decide:**

- Capture all levels vs. apply a minimum threshold at the provider.
- Report logs without a correlation ID (e.g., startup logs) or only logs tied to a scenario.
- Bridge NSB's own internal log abstraction (`LogManager`) via `NServiceBus.Extensions.Logging`
  to also capture framework-level log lines.

### Done conditions (larger)

Tests currently manually sequence `ExecuteScenarioAsync` + multiple `WaitFor*Async` calls.
A `DoneCondition` abstraction ("I'm done when handler X ran AND message Y was published")
would make test intent clearer and eliminate the need to know the right order to await.

### Multi-architecture Docker images (small)

The Dockerfiles target `mcr.microsoft.com/dotnet/runtime:10.0` which defaults to AMD64 on
Docker Desktop. This works fine on macOS Apple Silicon (Docker Desktop transparently emulates
AMD64) but adds overhead. A future improvement could build multi-arch images or detect the
host architecture.

### Saga data and message content in events (medium)

Let tests use saga data and message payloads inside `until` predicates and assertions:

```csharp
.SagaInvoked("SomeReplySaga", evt => evt.SagaData<SomeReplySagaData>().LoopCount >= 5)
var data = results.SagaInvoked("SomeReplySaga").SagaData<SomeReplySagaData>();
Assert.That(data.LoopCount, Is.EqualTo(5));
```

#### How to access saga state in the reporting behavior

`ActiveSagaInstance` is available via context extensions inside a pipeline behavior.
`ActiveSagaInstance.Instance.Entity` gives the concrete saga data object (confirmed via
`src/NServiceBus.IntegrationTesting/InterceptInvokedHandlers.cs`).

```csharp
if (context.Extensions.TryGet<ActiveSagaInstance>(out var activeSaga))
{
    var sagaData = activeSaga.Instance.Entity;
    sagaDataJson = JsonSerializer.Serialize(sagaData, sagaData.GetType());
}
```

#### Preferred approach: always include serialized payload (Option A)

- Extend `HandlerInvokedMessage` proto with `string message_json` and `string saga_data_json`
- Serialize `context.Message.Instance` → `message_json` in `ReportingBehavior`
- Serialize `activeSaga.Instance.Entity` → `saga_data_json` when saga is active
- Extend `HandlerInvokedEvent` record with `string? MessageJson` and `string? SagaDataJson`
- Add `T Message<T>()` and `T SagaData<T>()` helpers on `HandlerInvokedEvent` that call
  `JsonSerializer.Deserialize<T>`

Serialization overhead is negligible in test scenarios. Tests already reference message and
saga data assemblies (needed to know type names anyway), so typed deserialization is free.

Alternative (Option B): user-registered per-handler extractors in the agent bootstrap —
more surgical, zero overhead unless opted in, but requires extra setup wiring.

### Scenario-scoped timeout rules (considered, deferred)

`TimeoutRule` currently applies to all scenarios on an endpoint. A scenario-scoped variant
was considered: `.WhenScenario("Name")` on the rule, with the scenario name flowing via a
second `AsyncLocal<string?>` alongside `CurrentCorrelationId` in `AgentService`.

**Decided against for now.** The realistic use case (same timeout type, different reschedule
delay per scenario) is rare enough that the better remedy is to give each scenario its own
timeout message type. Adding scenario scoping to `TimeoutRule` would add complexity to the
API and the behavior without a concrete driving use case. Revisit if one emerges.

### Child-process endpoint option (medium, deferred until concrete use case)

Some endpoints cannot be containerised (legacy dependencies, COM interop, machine-local
resources, etc.). These could run as child processes on the test host machine instead of
Docker containers.

**Agent side: zero changes needed.** The agent reads `NSBUS_TESTING_HOST` and connects via
gRPC regardless of how the process was launched.

**`TestHostServer` side: already ready.** `Address` (`http://localhost:{port}`) works for
local processes; `ContainerAddress` (`http://host.docker.internal:{port}`) is the container
variant. No changes needed.

**Work required in `TestEnvironmentBuilder`/`TestEnvironment`:**

- Add `AddProcessEndpoint(name, executablePath, envVars?)` alongside `AddEndpoint`.
- On `StartAsync`, spawn the process via `System.Diagnostics.Process.Start()` with:
  - `NSBUS_TESTING_HOST = testHost.Address` (localhost, not container address)
  - Infrastructure connection strings via **host-exposed ports**, not Docker-network aliases
    (e.g. `rabbitMq.ConnectionString` from Testcontainers, not `host=rabbitmq`)
- Track processes in `TestEnvironment` and kill them in `DisposeAsync`.

The infrastructure routing (same RabbitMQ/PostgreSQL container, two different connection
string flavours depending on caller) is the main non-trivial piece. Container endpoints
get Docker-alias strings; process endpoints get host-port strings.

Deferred until a concrete use case drives it.

### HTTP mocking / external service stubs (implemented: Option A)

When a saga or handler performs an HTTP call to an external service, tests need a way to
stub those calls. Three options were considered:

#### Option A — embedded WireMock.Net in test process (IMPLEMENTED)

`WireMock.Net` starts an HTTP server inside the test process on a random port. Endpoint
containers reach it via `http://host.docker.internal:{port}` (injected as `WIREMOCK_URL`).

**Prerequisite**: endpoint configs must read external service base URLs from environment
variables (12-factor). Production sets a real URL; the `*.Testing` project inherits
`WIREMOCK_URL` automatically.

**Usage:**

```csharp
_env = await new TestEnvironmentBuilder()
    .UseWireMock()          // starts embedded server; sets WIREMOCK_URL for all containers
    ...
    .StartAsync();

// In the test:
_env.WireMock?.Given(Request.Create().WithPath("/api/foo").UsingGet())
    .RespondWith(Response.Create().WithStatusCode(200).WithBody("{}"));
```

`TestEnvironment.WireMock` is `null` when `UseWireMock()` was not called.

**Pros**: no extra Docker image, instant startup, rich request/response API, built-in
verification (`LogEntries`). **Cons**: in-process so crashes in WireMock configuration
can affect the test process; HTTP only (no HTTPS by default in this configuration).

#### Option B — custom stub service as a Docker container (not implemented, deferred)

Ship a dedicated lightweight HTTP stub image (e.g. a minimal ASP.NET app) that reads a
stub configuration file mounted at startup. Register it via `AddStubService(image, port)`.

**Pros**: full network isolation, closer to production. **Cons**: slower startup (container
build + start), more complex configuration API than WireMock.Net's fluent DSL, harder to
inspect calls from the test process without an additional control channel.

Deferred: Option A covers the common case with far less complexity.

#### Option C — in-process fake service via `IHostedService` / `BackgroundService` (not implemented, deferred)

Start a minimal Kestrel endpoint inside the test process (similar to `TestHostServer`) that
serves stub responses. Endpoint containers reach it via `host.docker.internal:{port}`.

**Pros**: zero extra dependencies, full .NET type system for request matching. **Cons**:
must be hand-written per project (no off-the-shelf stub DSL); essentially reimplements
WireMock.Net without the polish.

Deferred: Option A already provides a mature, production-proven implementation of exactly
this pattern.

### Channel fragility with concurrent tests (easy fix, not yet needed)

`WaitForHandlerInvocationAsync` uses a fan-out channel per waiter, filtered by correlation
ID, so multiple concurrent tests are already safe. Verify this holds for
`WaitForMessageDispatchedAsync` and `WaitForMessageFailureAsync` as well.
