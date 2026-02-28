# Getting Started with NServiceBus.IntegrationTesting

This guide walks you through adding integration tests to an existing NServiceBus solution using
NServiceBus.IntegrationTesting. By the end you will have:

- A companion `*.Testing` project for each endpoint you want to test
- One or more named **scenarios** that send messages from inside the endpoint process
- An NUnit test project that starts Docker containers and asserts on handler invocations,
  message dispatches, saga state, and failure outcomes

---

## What is NServiceBus.IntegrationTesting?

NServiceBus.IntegrationTesting runs your real NServiceBus endpoints as Docker containers and
lets your tests observe what happens — which handlers ran, which messages were dispatched,
which sagas were started, and whether any messages failed permanently.

Unlike unit tests or acceptance tests that mock the transport, every message here travels
over the **real transport** (e.g. RabbitMQ) and is persisted by the **real persistence**
(e.g. PostgreSQL). This gives you high confidence that your production configuration is
correct.

### How it works

```text
┌──────────────────────────────────────────┐
│  Test process                            │
│  TestHostServer (gRPC, dynamic port)     │
└──────────────────┬───────────────────────┘
         bidirectional streaming
                   │
        ┌──────────┴────────────┐
        │                       │
┌───────▼─────────┐   ┌─────────▼────────┐
│  YourEndpoint   │   │  AnotherEndpoint │
│  container      │   │  container       │
└─────────────────┘   └──────────────────┘
        │                       │
    RabbitMQ container    PostgreSQL container
```

1. The test process starts a lightweight **gRPC host** on a dynamic port.
2. Infrastructure containers (RabbitMQ, PostgreSQL) start on a shared Docker network.
3. Each endpoint's **companion `*.Testing` image** is built and started as a container.
4. Each endpoint embeds a gRPC **agent** that connects home on startup.
5. A test **scenario** — a named entry point in the `*.Testing` project — is executed
   inside the endpoint process using the real `IMessageSession`.
6. The agent streams back handler invocations, saga events, message dispatches, and
   failure events to the test host.
7. The test awaits conditions via the fluent `ObserveContext` API and then makes assertions.

---

## Prerequisites

| Requirement | Minimum version |
|---|---|
| .NET SDK | 9.0 |
| Docker Desktop (macOS/Windows) or Docker Engine (Linux) | 20.10 |
| NUnit | 4.x |

> **Tip for Linux CI**: add `WithExtraHost("host.docker.internal", "host-gateway")` to your
> endpoint container builder if containers cannot resolve `host.docker.internal`. This is
> already done automatically by `TestEnvironmentBuilder`.

---

## Project structure

The framework enforces a clean boundary between production code and test infrastructure:

```text
YourEndpoint/                   ← production code, zero test dependencies
  YourEndpointConfig.cs         ← static Create() factory (reused by *.Testing)
  Handlers/
    SomeMessageHandler.cs

YourEndpoint.Testing/           ← wraps production config, adds agent + scenarios
  Program.cs                    ← IntegrationTestingBootstrap.RunAsync(...)
  SomeMessageScenario.cs        ← implements Scenario base class
  Dockerfile                    ← builds the testable container image

YourEndpoint.Tests/             ← NUnit test project
  WhenSomeMessageIsSent.cs      ← test fixture
```

- **`YourEndpoint/`** has no reference to any testing library and is deployed as-is to production.
- **`YourEndpoint.Testing/`** only runs inside integration tests. It imports the production
  `Create()` factory and layers on test-specific settings (e.g. fewer retries).
- **`YourEndpoint.Tests/`** references `YourEndpoint.Testing` with
  `ReferenceOutputAssembly="false"` — this causes the companion project to be compiled as
  part of the test build (so errors are caught early) but the assembly itself is never
  loaded by the test process. The Docker image is built at test runtime.

---

## Step 1 — Create the production endpoint configuration factory

If your production endpoint does not already have a static configuration factory, create one.
The factory reads connection strings from environment variables so it works both in production
and inside Docker containers:

```csharp
// YourEndpoint/YourEndpointConfig.cs
public static class YourEndpointConfig
{
    public static EndpointConfiguration Create()
    {
        var rabbitMqConnectionString =
            Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING")
            ?? "host=localhost;username=guest;password=guest";

        var config = new EndpointConfiguration("YourEndpoint");

        var transport = new RabbitMQTransport(
            RoutingTopology.Conventional(QueueType.Quorum),
            rabbitMqConnectionString);

        var routing = config.UseTransport(transport);
        routing.RouteToEndpoint(typeof(SomeCommand), "YourEndpoint");

        config.UseSerialization<SystemJsonSerializer>();
        config.EnableInstallers();

        return config;
    }
}
```

> **Important**: read all connection strings from environment variables. The framework
> injects the correct container-network addresses (e.g. `host=rabbitmq`) at startup.

---

## Step 2 — Create the companion `*.Testing` project

Add a new console project alongside the production endpoint:

```xml
<!-- YourEndpoint.Testing/YourEndpoint.Testing.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\YourEndpoint\YourEndpoint.csproj" />
    <!--
      Reference the agent version that matches your NServiceBus major version:
        NServiceBus 10 → NServiceBus.IntegrationTesting.Agent.v10
        NServiceBus 9  → NServiceBus.IntegrationTesting.Agent.v9
    -->
    <ProjectReference Include="..\NServiceBus.IntegrationTesting.Agent.v10\NServiceBus.IntegrationTesting.Agent.v10.csproj" />
  </ItemGroup>
</Project>
```

The entry point calls `IntegrationTestingBootstrap.RunAsync`, passing the production factory
and a list of scenarios:

```csharp
// YourEndpoint.Testing/Program.cs
using NServiceBus.IntegrationTesting.Agent;
using YourEndpoint;

await IntegrationTestingBootstrap.RunAsync(
    "YourEndpoint",
    YourEndpointConfig.Create,
    scenarios: [new SomeCommandScenario()]);
```

### Overlaying test-specific settings

You can adjust the configuration before handing it off — for example, reducing retries so
that failing messages reach the error queue quickly instead of spending minutes in retry loops:

```csharp
await IntegrationTestingBootstrap.RunAsync(
    "YourEndpoint",
    CreateConfig);

static EndpointConfiguration CreateConfig()
{
    var config = YourEndpointConfig.Create();
    config.Recoverability().Immediate(s => s.NumberOfRetries(0));
    config.Recoverability().Delayed(s => s.NumberOfRetries(0));
    return config;
}
```

> **Never modify the production `Create()` factory for testing purposes.** Wrap it in the
> `*.Testing` project and overlay settings there.

---

## Step 3 — Write scenarios

A **scenario** is a named entry point that runs _inside the endpoint process_ using the
real `IMessageSession`. This means messages are sent with the real serializer, real routing,
and real transport — no mocking involved.

```csharp
// YourEndpoint.Testing/SomeCommandScenario.cs
using NServiceBus;
using NServiceBus.IntegrationTesting.Agent;
using YourEndpoint.Messages;

public class SomeCommandScenario : Scenario
{
    public override string Name => "SomeCommand";

    public override async Task Execute(
        IMessageSession session,
        Dictionary<string, string> args,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.Parse(args["ID"]);
        await session.Send(new SomeCommand { Id = id });
    }
}
```

Key points:

- `Name` must be unique within an endpoint and is used by the test to trigger the scenario.
- `args` is a `Dictionary<string, string>` passed from the test — use it for correlation IDs
  and any other per-test data.
- The `Execute` method runs inside the endpoint process. Any message type available there
  is available here; no special constructors or test-only types are needed.

---

## Step 4 — Write the Dockerfile for the companion project

The Dockerfile builds the `*.Testing` project (not the production project) into a container
image. The build context is the `src/` directory so `COPY` instructions can reach all sibling
projects.

```dockerfile
# YourEndpoint.Testing/Dockerfile
# Build context: src/
# docker build -f YourEndpoint.Testing/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer-cached restore
COPY NServiceBus.IntegrationTesting.Agent.v10/NServiceBus.IntegrationTesting.Agent.v10.csproj NServiceBus.IntegrationTesting.Agent.v10/
COPY YourMessages/YourMessages.csproj YourMessages/
COPY YourEndpoint/YourEndpoint.csproj YourEndpoint/
COPY YourEndpoint.Testing/YourEndpoint.Testing.csproj YourEndpoint.Testing/
COPY proto/ proto/

RUN dotnet restore YourEndpoint.Testing/YourEndpoint.Testing.csproj

# Copy source and publish
COPY NServiceBus.IntegrationTesting.Agent.v10/ NServiceBus.IntegrationTesting.Agent.v10/
COPY YourMessages/ YourMessages/
COPY YourEndpoint/ YourEndpoint/
COPY YourEndpoint.Testing/ YourEndpoint.Testing/

RUN dotnet publish YourEndpoint.Testing/YourEndpoint.Testing.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "YourEndpoint.Testing.dll"]
```

> **Do not use `--no-restore`** on the `dotnet publish` step. On ARM64/Apple Silicon with
> Linux/AMD64 containers, the restore and publish must share the same RID context.

---

## Step 5 — Create the test project

Create an NUnit test project that references both the framework and the companion projects:

```xml
<!-- YourEndpoint.Tests/YourEndpoint.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="NUnit" Version="4.2.2" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Testcontainers" Version="4.10.0" />
  </ItemGroup>

  <ItemGroup>
    <!-- The framework library — provides TestEnvironmentBuilder, ObserveContext, etc. -->
    <ProjectReference Include="..\NServiceBus.IntegrationTesting\NServiceBus.IntegrationTesting.csproj" />

    <!--
      ReferenceOutputAssembly="false": compile the companion project (catches errors early)
      but do not load its assembly at runtime — Docker builds the image instead.
    -->
    <ProjectReference Include="..\YourEndpoint.Testing\YourEndpoint.Testing.csproj"
                      ReferenceOutputAssembly="false"
                      Private="false" />
  </ItemGroup>
</Project>
```

---

## Step 6 — Write test fixtures

### Basic fixture structure

Each test fixture manages a shared `TestEnvironment` that is started once for the entire
fixture and torn down afterward. Starting the environment (building Docker images, starting
containers) is expensive, so share it across all tests in the fixture.

```csharp
using NServiceBus.IntegrationTesting;
using NUnit.Framework;

[TestFixture]
[NonParallelizable]
public class WhenSomeCommandIsSent
{
    static TestEnvironment _env = null!;
    static EndpointHandle _yourEndpoint = null!;

    [OneTimeSetUp]
    public static async Task SetUp()
    {
        // Point to the directory that contains the endpoint sub-directories.
        // Typically this is the 'src/' folder of your repository.
        var srcDir = Path.Combine(FindRepoRoot(), "src");

        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMq()
            .UsePostgreSql()
            .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
            .StartAsync();

        _yourEndpoint = _env.GetEndpoint("YourEndpoint");
    }

    [OneTimeTearDown]
    public static Task TearDown() => _env.DisposeAsync().AsTask();

    [Test]
    public async Task Handler_should_be_invoked()
    {
        var correlationId = await _yourEndpoint.ExecuteScenarioAsync(
            "SomeCommand",
            new Dictionary<string, string> { { "ID", Guid.NewGuid().ToString() } });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var results = await _env.Observe(correlationId, cts.Token)
            .HandlerInvoked("SomeMessageHandler")
            .WhenAllAsync();

        Assert.That(
            results.HandlerInvoked("SomeMessageHandler").EndpointName,
            Is.EqualTo("YourEndpoint"));
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetDirectories(".git").Any())
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException(
                "Cannot locate repository root. Ensure the test runs inside a git repository.");
    }
}
```

### Executing a scenario

```csharp
var correlationId = await _yourEndpoint.ExecuteScenarioAsync(
    "SomeCommand",
    new Dictionary<string, string> { { "ID", Guid.NewGuid().ToString() } });
```

- `ExecuteScenarioAsync` returns a **correlation ID** — a string that ties all events
  produced by this scenario execution together.
- The `args` dictionary is forwarded to `Scenario.Execute` inside the endpoint process.
- Always generate a fresh `Guid` for each test run so events from different tests are
  never mixed up.

---

## Step 7 — Observing results

The `ObserveContext` API lets you declare what you are waiting for before calling
`WhenAllAsync()`. Because all listeners are registered immediately, no event can be missed
between registrations — even if a handler runs extremely fast.

> **Critical**: register **all** conditions before awaiting `WhenAllAsync()`.

### Waiting for a handler invocation

```csharp
var results = await _env.Observe(correlationId, cts.Token)
    .HandlerInvoked("SomeMessageHandler")
    .HandlerInvoked("AnotherMessageHandler")
    .WhenAllAsync();

// The last (or only) invocation of the handler:
var evt = results.HandlerInvoked("SomeMessageHandler");
Assert.That(evt.EndpointName, Is.EqualTo("YourEndpoint"));
```

The string passed to `HandlerInvoked` is the **simple type name** of the handler class
(e.g. `"SomeMessageHandler"` for `class SomeMessageHandler : IHandleMessages<SomeMessage>`).

### Waiting for a saga invocation

Sagas are tracked separately from plain handlers so you can distinguish between the two:

```csharp
var results = await _env.Observe(correlationId, cts.Token)
    .SagaInvoked("OrderSaga")
    .WhenAllAsync();

var sagaEvt = results.SagaInvoked("OrderSaga");
Assert.Multiple(() =>
{
    Assert.That(sagaEvt.IsSaga, Is.True);
    Assert.That(sagaEvt.SagaIsNew, Is.True);
    Assert.That(sagaEvt.SagaIsCompleted, Is.False);
});
```

Fields available on a `HandlerInvokedEvent` for sagas:

| Field | Description |
|---|---|
| `EndpointName` | Logical endpoint name |
| `IsSaga` | Always `true` for saga invocations |
| `SagaIsNew` | `true` if the saga instance was created by this message |
| `SagaIsCompleted` | `true` if `MarkAsComplete()` was called |
| `SagaNotFound` | `true` if no existing instance matched |
| `SagaId` | The saga data `Id` |

### Waiting for a message dispatch

```csharp
var results = await _env.Observe(correlationId, cts.Token)
    .MessageDispatched("AnotherMessage")
    .WhenAllAsync();

var dispatch = results.MessageDispatched("AnotherMessage");
Assert.Multiple(() =>
{
    Assert.That(dispatch.EndpointName, Is.EqualTo("YourEndpoint"));
    Assert.That(dispatch.Intent, Is.EqualTo("Send"));   // or "Publish", "Reply", "RequestTimeout"
});
```

The string passed to `MessageDispatched` is the **simple type name** of the message class.

### Waiting for a message failure

Use `MessageFailed()` alone when you expect a message to be permanently sent to the error
queue. It cannot be combined with success conditions:

```csharp
var results = await _env.Observe(correlationId, cts.Token)
    .MessageFailed()
    .WhenAllAsync();

var failure = results.MessageFailed();
Assert.Multiple(() =>
{
    Assert.That(failure.EndpointName, Is.EqualTo("AnotherEndpoint"));
    Assert.That(failure.ExceptionMessage, Does.Contain("expected error text"));
});
```

> **Tip**: set `NumberOfRetries(0)` in the `*.Testing` project so messages fail
> immediately rather than spending time in retry loops:
>
> ```csharp
> config.Recoverability().Immediate(s => s.NumberOfRetries(0));
> config.Recoverability().Delayed(s => s.NumberOfRetries(0));
> ```

---

## Advanced topics

### Multi-endpoint scenarios

Add as many endpoints as you need. Each must have its own `*.Testing` project and Dockerfile:

```csharp
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRabbitMq()
    .UsePostgreSql()
    .AddEndpoint("OrdersEndpoint", "OrdersEndpoint.Testing/Dockerfile")
    .AddEndpoint("BillingEndpoint", "BillingEndpoint.Testing/Dockerfile")
    .AddEndpoint("ShippingEndpoint", "ShippingEndpoint.Testing/Dockerfile")
    .StartAsync();
```

Endpoints in the same test environment share the Docker network. They can send messages to
each other exactly as they would in production.

### Testing saga timeouts

Sagas that use `RequestTimeout` may have delays that are far too long for tests (minutes or
hours in production). Use `TimeoutRule` to shorten them in the `*.Testing` project:

```csharp
// YourEndpoint.Testing/Program.cs
await IntegrationTestingBootstrap.RunAsync(
    "YourEndpoint",
    YourEndpointConfig.Create,
    scenarios: [new SomeCommandScenario()],
    timeoutRules: [TimeoutRule.For<OrderProcessingTimeout>(TimeSpan.FromSeconds(5))]);
```

Then wait for the timeout-triggered handler in your test:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

var results = await _env.Observe(correlationId, cts.Token)
    .SagaInvoked("OrderSaga")
    .MessageDispatched("OrderProcessingTimeout")
    .HandlerInvoked("TimeoutCompletionHandler")
    .WhenAllAsync();

Assert.That(results.MessageDispatched("OrderProcessingTimeout").Intent,
    Is.EqualTo("RequestTimeout"));
```

### Predicate overloads

All `HandlerInvoked`, `SagaInvoked`, and `MessageDispatched` conditions support two
additional overloads that let you encode business assertions directly in the done condition.

**Single-event predicate** — the condition fires only when the predicate on the latest event
returns `true`:

```csharp
// Only fires when the saga is new — no need to assert afterward
var results = await _env.Observe(correlationId, cts.Token)
    .SagaInvoked("OrderSaga", evt => evt.SagaIsNew)
    .WhenAllAsync();
```

**List predicate** — the condition fires when the accumulated list of matching events
satisfies the predicate. Use this when you need to reason about multiple events together:

```csharp
// Wait until we have seen at least 3 dispatches of the same type
var results = await _env.Observe(correlationId, cts.Token)
    .MessageDispatched("OrderStatusUpdated", all => all.Count >= 3)
    .WhenAllAsync();

var dispatches = results.MessageDispatches("OrderStatusUpdated");
Assert.That(dispatches, Has.Count.GreaterThanOrEqualTo(3));
```

### Stubbing external HTTP services with WireMock

If your endpoint calls an external HTTP service, use `.UseWireMock()` to start an embedded
[WireMock.Net](https://github.com/WireMock-Net/WireMock.Net) stub server. The framework
automatically injects `WIREMOCK_URL` into every endpoint container.

```csharp
// *.Tests.csproj: add <PackageReference Include="WireMock.Net" Version="1.25.0" />

_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRabbitMq()
    .UseWireMock()                       // starts the stub server
    .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
    .StartAsync();
```

In your endpoint, read `WIREMOCK_URL` from the environment:

```csharp
// Only calls the external service when the variable is set (i.e. in test mode)
var externalUrl = Environment.GetEnvironmentVariable("WIREMOCK_URL");
if (externalUrl is not null)
{
    var response = await _http.GetStringAsync($"{externalUrl}/api/data", ct);
}
```

In the test, configure the stub before triggering the scenario, then verify the request
was received afterward:

```csharp
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

[Test]
public async Task Handler_calls_external_service()
{
    // Configure stub first so no request is missed
    _env.WireMock!
        .Given(Request.Create().WithPath("/api/data").UsingGet())
        .RespondWith(Response.Create().WithStatusCode(200).WithBody("ok"));

    var correlationId = await _yourEndpoint.ExecuteScenarioAsync(
        "SomeCommand",
        new Dictionary<string, string> { { "ID", Guid.NewGuid().ToString() } });

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    await _env.Observe(correlationId, cts.Token)
        .HandlerInvoked("SomeMessageHandler")
        .WhenAllAsync();

    Assert.That(
        _env.WireMock!.LogEntries.Any(e => e.RequestMessage.Path == "/api/data"),
        Is.True);
}
```

### Dumping container logs on failure

When a test fails, endpoint container logs are invaluable for diagnosing what went wrong.
Add a `[TearDown]` method that dumps them only on failure:

```csharp
[TearDown]
public async Task DumpContainerLogsOnFailure()
{
    if (TestContext.CurrentContext.Result.Outcome.Status
            != NUnit.Framework.Interfaces.TestStatus.Failed)
        return;

    var (stdout, stderr) = await _env.GetEndpointContainerLogsAsync("YourEndpoint");
    TestContext.Out.WriteLine("=== YourEndpoint stdout ===");
    TestContext.Out.WriteLine(stdout);
    TestContext.Out.WriteLine("=== YourEndpoint stderr ===");
    TestContext.Out.WriteLine(stderr);
}
```

### Adjusting the agent connection timeout

By default, `StartAsync` waits up to 120 seconds for all endpoint agents to connect. Adjust
this if your Docker image builds or container startup are particularly slow:

```csharp
_env = await new TestEnvironmentBuilder()
    .WithDockerfileDirectory(srcDir)
    .UseRabbitMq()
    .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
    .WithAgentConnectionTimeout(TimeSpan.FromMinutes(5))
    .StartAsync();
```

---

## Complete example

The following end-to-end example mirrors the sample included with this repository. It shows
two endpoints, a saga with a timeout, a failure scenario, and WireMock stubbing.

### Production endpoint (`SampleEndpoint/`)

```csharp
// SampleEndpoint/SampleEndpointConfig.cs
public static class SampleEndpointConfig
{
    public static EndpointConfiguration Create()
    {
        var rabbitMq = Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING")
            ?? "host=localhost;username=guest;password=guest";
        var postgres = Environment.GetEnvironmentVariable("POSTGRESQL_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

        var config = new EndpointConfiguration("SampleEndpoint");

        var routing = config.UseTransport(
            new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), rabbitMq));
        routing.RouteToEndpoint(typeof(AnotherMessage), "AnotherEndpoint");

        var persistence = config.UsePersistence<SqlPersistence>();
        var dialect = persistence.SqlDialect<SqlDialect.PostgreSql>();
        dialect.JsonBParameterModifier(p =>
        {
            var np = (NpgsqlParameter)p;
            np.NpgsqlDbType = NpgsqlDbType.Jsonb;
        });
        persistence.ConnectionBuilder(() => new NpgsqlConnection(postgres));

        config.UseSerialization<SystemJsonSerializer>();
        config.EnableInstallers();
        return config;
    }
}
```

### Companion project (`SampleEndpoint.Testing/`)

```csharp
// SampleEndpoint.Testing/Program.cs
using NServiceBus.IntegrationTesting.Agent;
using SampleEndpoint;
using SampleEndpoint.Handlers;
using SampleEndpoint.Testing;

await IntegrationTestingBootstrap.RunAsync(
    "SampleEndpoint",
    SampleEndpointConfig.Create,
    scenarios: [new SomeMessageScenario(), new FailingMessageScenario()],
    timeoutRules: [TimeoutRule.For<SomeReplySagaTimeout>(TimeSpan.FromSeconds(5))]);
```

```csharp
// SampleEndpoint.Testing/SomeMessageScenario.cs
public class SomeMessageScenario : Scenario
{
    public override string Name => "SomeMessage";

    public override async Task Execute(
        IMessageSession session,
        Dictionary<string, string> args,
        CancellationToken cancellationToken = default)
        => await session.Send(new SomeMessage { Id = Guid.Parse(args["ID"]) });
}
```

### Test fixture (`SampleEndpoint.Tests/`)

```csharp
// SampleEndpoint.Tests/WhenSomeMessageIsSent.cs
[TestFixture]
[NonParallelizable]
public class WhenSomeMessageIsSent
{
    static TestEnvironment _env = null!;
    static EndpointHandle _sampleEndpoint = null!;

    [OneTimeSetUp]
    public static async Task SetUp()
    {
        var srcDir = Path.Combine(FindRepoRoot(), "src");

        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMq()
            .UsePostgreSql()
            .AddEndpoint("SampleEndpoint", "SampleEndpoint.Testing/Dockerfile")
            .AddEndpoint("AnotherEndpoint", "AnotherEndpoint.Testing/Dockerfile")
            .StartAsync();

        _sampleEndpoint = _env.GetEndpoint("SampleEndpoint");
    }

    [TearDown]
    public async Task DumpContainerLogsOnFailure()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status
                != NUnit.Framework.Interfaces.TestStatus.Failed)
            return;

        var (stdout, stderr) = await _env.GetEndpointContainerLogsAsync("SampleEndpoint");
        TestContext.Out.WriteLine(stdout);
        TestContext.Out.WriteLine(stderr);
    }

    [OneTimeTearDown]
    public static Task TearDown() => _env.DisposeAsync().AsTask();

    [Test]
    public async Task The_full_chain_should_be_processed()
    {
        var correlationId = await _sampleEndpoint.ExecuteScenarioAsync(
            "SomeMessage",
            new Dictionary<string, string> { { "ID", Guid.NewGuid().ToString() } });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var results = await _env.Observe(correlationId, cts.Token)
            .HandlerInvoked("SomeMessageHandler")
            .HandlerInvoked("AnotherMessageHandler")
            .HandlerInvoked("SomeReplyHandler")
            .WhenAllAsync();

        Assert.Multiple(() =>
        {
            Assert.That(results.HandlerInvoked("SomeMessageHandler").EndpointName,
                Is.EqualTo("SampleEndpoint"));
            Assert.That(results.HandlerInvoked("AnotherMessageHandler").EndpointName,
                Is.EqualTo("AnotherEndpoint"));
        });
    }

    [Test]
    public async Task A_failing_message_is_reported()
    {
        var correlationId = await _sampleEndpoint.ExecuteScenarioAsync(
            "FailingMessage",
            new Dictionary<string, string> { { "ID", Guid.NewGuid().ToString() } });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var results = await _env.Observe(correlationId, cts.Token)
            .MessageFailed()
            .WhenAllAsync();

        var failure = results.MessageFailed();
        Assert.That(failure.EndpointName, Is.EqualTo("AnotherEndpoint"));
        Assert.That(failure.ExceptionMessage, Does.Contain("Intentional failure"));
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetDirectories(".git").Any())
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Cannot locate repository root.");
    }
}
```

---

## Troubleshooting

### Agent does not connect within the timeout

- Increase `WithAgentConnectionTimeout` for slower machines or CI environments.
- Check that `NSBUS_TESTING_HOST` is not blocked by a firewall rule on the host.
- Ensure `WithExtraHost("host.docker.internal", "host-gateway")` resolves correctly on
  Linux Docker Engine (this is applied automatically by `TestEnvironmentBuilder`).
- Dump the container logs for the failing endpoint to see the startup error:
  ```csharp
  var (out, err) = await _env.GetEndpointContainerLogsAsync("YourEndpoint");
  ```

### Docker image build fails

- Verify the build context (`WithDockerfileDirectory`) points to the directory that
  contains all the projects referenced in the Dockerfile's `COPY` instructions.
- Add a `.dockerignore` file in the build context directory that excludes `**/bin/`
  and `**/obj/` to prevent local build artifacts from contaminating container builds.

### Tests pass locally but fail in CI

- Ensure Docker images are pre-built in a CI step before the test run. See the CI
  workflow for the pattern of building images with a fixed tag before running tests.
- On Linux CI, the BuildKit async-tagging race condition means the image tag may not
  be immediately visible after `docker build`. Pre-building images with the exact same
  tag that `TestEnvironmentBuilder` uses (`localhost/nsb-integration-testing/{name}:latest`)
  avoids this.

### `OperationCanceledException` — conditions not satisfied in time

- Increase the `CancellationTokenSource` timeout. Long-running tests (e.g. saga timeouts)
  need enough headroom.
- Make sure you have registered all expected conditions before calling `WhenAllAsync()`.
- If a handler is not being invoked, check the routing configuration and that the message
  type name matches exactly (simple type name, not fully qualified).

### `InvalidOperationException: Cannot register HandlerInvoked() when MessageFailed() is already registered`

`MessageFailed()` must be the only condition on an `ObserveContext`. Create a separate
test for the failure scenario instead of mixing success and failure conditions.

---

## API reference summary

### `TestEnvironmentBuilder`

| Method | Description |
|---|---|
| `.WithDockerfileDirectory(path)` | Sets the Docker build context root (required) |
| `.UseRabbitMq(image?)` | Starts a RabbitMQ container; injects `RABBITMQ_CONNECTION_STRING` |
| `.UsePostgreSql(image?)` | Starts a PostgreSQL container; injects `POSTGRESQL_CONNECTION_STRING` |
| `.UseWireMock()` | Starts embedded WireMock stub server; injects `WIREMOCK_URL` |
| `.AddEndpoint(name, dockerfile)` | Registers an endpoint container to build and start |
| `.WithAgentConnectionTimeout(ts)` | Overrides the 120 s default connection wait |
| `.StartAsync()` | Builds and starts everything; returns `TestEnvironment` |

### `TestEnvironment`

| Member | Description |
|---|---|
| `GetEndpoint(name)` | Returns an `EndpointHandle` for the named endpoint |
| `Observe(correlationId, ct)` | Returns an `ObserveContext` for the given correlation ID |
| `WireMock` | The embedded `WireMockServer`, or `null` if not configured |
| `GetEndpointContainerLogsAsync(name)` | Returns `(stdout, stderr)` for the named container |
| `DisposeAsync()` | Stops and disposes all containers and the Docker network |

### `EndpointHandle`

| Member | Description |
|---|---|
| `EndpointName` | The logical name of the endpoint |
| `ExecuteScenarioAsync(name, args?, ct?)` | Triggers a scenario; returns the correlation ID |

### `ObserveContext`

| Method | Description |
|---|---|
| `.HandlerInvoked(name)` | Wait for the first invocation of the named handler |
| `.HandlerInvoked(name, Func<TEvent,bool>)` | Wait until the single-event predicate is satisfied |
| `.HandlerInvoked(name, Func<IReadOnlyList<TEvent>,bool>)` | Wait until the list predicate is satisfied |
| `.SagaInvoked(name)` | Same as `HandlerInvoked` but for saga invocations |
| `.MessageDispatched(name)` | Wait for the first dispatch of the named message type |
| `.MessageDispatched(name, predicate)` | Single-event or list predicate overloads |
| `.MessageFailed()` | Wait for a permanent failure (use alone) |
| `.WhenAllAsync()` | Await all conditions; returns `ObserveResults` |

### `ObserveResults`

| Method | Description |
|---|---|
| `HandlerInvoked(name)` | Last (or only) `HandlerInvokedEvent` for the handler |
| `HandlerInvocations(name)` | All collected `HandlerInvokedEvent` instances |
| `SagaInvoked(name)` | Last (or only) saga `HandlerInvokedEvent` |
| `SagaInvocations(name)` | All collected saga `HandlerInvokedEvent` instances |
| `MessageDispatched(name)` | Last (or only) `MessageDispatchedEvent` |
| `MessageDispatches(name)` | All collected `MessageDispatchedEvent` instances |
| `MessageFailed()` | The `MessageFailedEvent` |

### `IntegrationTestingBootstrap.RunAsync`

```csharp
await IntegrationTestingBootstrap.RunAsync(
    endpointName:      "YourEndpoint",
    configFactory:     YourEndpointConfig.Create,
    scenarios:         [new SomeCommandScenario()],     // optional
    timeoutRules:      [TimeoutRule.For<T>(TimeSpan)],  // optional
    sigTermGracePeriod: TimeSpan.FromSeconds(10));       // optional, default 5 s
```

### `TimeoutRule`

```csharp
// Replace the scheduled delay for T with a fixed value
TimeoutRule.For<SomeTimeout>(TimeSpan.FromSeconds(5))

// Compute the delay from the timeout message instance
TimeoutRule.For<SomeTimeout>(msg => msg.CustomDelay)
```
