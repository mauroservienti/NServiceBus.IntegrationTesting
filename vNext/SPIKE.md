# vNext Spike — Out-of-Process Integration Testing with gRPC

## Goal

Prove a new architecture for `NServiceBus.IntegrationTesting` that eliminates the key
limitation of the existing framework: all endpoints must share the same NServiceBus package
version because they run in-process together.

The new architecture runs each endpoint in its own Docker container. The test process is
the gRPC server; each endpoint embeds a gRPC client agent that dials home on startup.

**Status: spike complete and green.** Multi-endpoint interaction (send → reply chain across
two containers) is proven end-to-end.

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
           RabbitMQ container
           (shared Docker network, alias "rabbitmq")
```

**Communication direction:**
- Agent → Host: `ConnectMessage`, `HandlerInvokedMessage`
- Host → Agent: `ReadyMessage`, `ExecuteScenarioMessage`

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
│   ├── ReportingBehavior.cs           # NSB pipeline behavior; fires HandlerInvokedMessage
│   └── Scenario.cs                    # Abstract base class for user-defined scenarios
│
├── NServiceBus.IntegrationTesting.Containers/
│   ├── TestHostServer.cs              # Starts Kestrel gRPC server; exposes Address/ContainerAddress
│   └── TestHostGrpcService.cs         # Server-side: WaitForAgentAsync, ExecuteScenarioAsync,
│                                      #   WaitForHandlerInvocationAsync
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
│       └── SomeReplyHandler.cs        # Handles SomeReply (the reply from AnotherEndpoint)
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
    ├── WhenSomeMessageIsSent.cs        # Full multi-endpoint test
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
    public override async Task Execute(IMessageSession session, string[] args, CancellationToken ct)
        => await session.Send(new SomeMessage { Id = Guid.NewGuid() });
}
```

Scenarios run **inside the endpoint process** using the real, fully-configured
`IMessageSession`. No cross-process serialization of message payloads occurs.

The test just passes the name: `ExecuteScenarioAsync("SampleEndpoint", "SomeMessage")`.

### 3. NServiceBus 10 constraints

- **No `IWantToRunWhenEndpointStartsAndStops`** in NSB 10 core (moved to NSB.Host).
  Agent connection is therefore explicit: `IntegrationTestingBootstrap` calls
  `agentService.ConnectAsync(endpointInstance)` after `Endpoint.Start()`.

- **No feature auto-discovery** — NSB 10 is moving away from assembly scanning toward
  trimming support. Defining a test agent as an NSB Feature that auto-registers isn't viable.
  Explicit opt-in via the `*.Testing` project pattern is the right approach.

- **Behaviors registered as instances**, not via DI:
  `configuration.Pipeline.Register(new ReportingBehavior(agentService), "description")`.

### 4. Container networking

- `TestHostServer` binds Kestrel to `IPAddress.Any` (not just `localhost`) so containers
  can reach it.
- The test passes `NSBUS_TESTING_HOST = http://host.docker.internal:{port}` to containers.
  `host.docker.internal` is resolved by Docker Desktop on macOS and Windows.
- RabbitMQ is reached by containers via the Docker network alias `rabbitmq` at the default
  port — no host port mapping needed for inter-container traffic.
- `TestHostServer.ContainerAddress` returns `http://host.docker.internal:{port}`;
  `TestHostServer.Address` returns `http://localhost:{port}` for in-process use.

### 5. Dockerfile: no `--no-restore` on publish

On Apple Silicon (ARM64) with Docker Desktop running Linux/AMD64 containers, using
`dotnet publish --no-restore` after a `dotnet restore` layer causes a native asset mismatch
(`NETSDK1064`). The restore and publish steps must use the same runtime, which they do when
`--no-restore` is omitted. The implicit re-restore is fast due to layer caching.

---

## Proven End-to-End Flow

```
Test                          SampleEndpoint          AnotherEndpoint
 │                                  │                       │
 │── ExecuteScenarioAsync ─────────►│                       │
 │   ("SampleEndpoint","SomeMessage")│                       │
 │                                  │                       │
 │                       [SomeMessageScenario.Execute]       │
 │                       session.Send(SomeMessage)           │
 │                                  │                       │
 │                       SomeMessageHandler                  │
 │◄── HandlerInvokedMessage ────────│                       │
 │    (SampleEndpoint,              │                       │
 │     SomeMessageHandler)          │                       │
 │                                  │── AnotherMessage ────►│
 │                                  │                       │
 │                                  │              AnotherMessageHandler
 │◄── HandlerInvokedMessage ────────┼───────────────────────│
 │    (AnotherEndpoint,             │                       │
 │     AnotherMessageHandler)       │◄── Reply(SomeReply) ──│
 │                                  │                       │
 │                       SomeReplyHandler                    │
 │◄── HandlerInvokedMessage ────────│                       │
 │    (SampleEndpoint,              │                       │
 │     SomeReplyHandler)            │                       │
 │                                  │                       │
 │ Assert all three invocations     │                       │
```

---

## Known Gaps / Next Steps

### Channel fragility (easy fix)
`WaitForHandlerInvocationAsync` reads from a single shared `Channel<HandlerInvokedEvent>`.
If two tests run concurrently and both expect the same handler type, they race for the same
event. Each test needs its own subscription (e.g., fan-out via `IAsyncEnumerable` broadcast,
or per-test channels registered before the scenario executes).

### Outgoing message tracking (medium)
`ReportingBehavior` currently only reports handler invocations. Extending it to also report
what each handler sent/published/replied would give tests full visibility into the message
flow — useful for asserting that the right messages were dispatched even in failure scenarios.
Requires intercepting `IMessageHandlerContext.Send/Publish/Reply` in the behavior.

### Saga state reporting (medium)
When a saga handles a message, the test should be able to inspect the saga's persisted state.
Requires hooking into NSB's saga persistence pipeline.

### Done conditions (larger)
Tests currently manually sequence `ExecuteScenarioAsync` + multiple `WaitForHandlerInvocationAsync`
calls. A `DoneCondition` abstraction ("I'm done when handler X ran AND message Y was published")
would make test intent clearer and eliminate the need to know the right order to await.

### Linux/AMD64 container assumption
The Dockerfiles target `mcr.microsoft.com/dotnet/runtime:10.0` which defaults to AMD64 on
Docker Desktop. This works fine on macOS Apple Silicon (Docker Desktop transparently emulates
AMD64) but adds overhead. A future improvement could build multi-arch images or detect the
host architecture.
