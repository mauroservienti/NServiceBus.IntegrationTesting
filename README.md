# NServiceBus.IntegrationTesting

NServiceBus.IntegrationTesting enables testing end-to-end business scenarios against real production endpoints, real transports, and real persistence.

> [!IMPORTANT]
> **Disclaimer**: NServiceBus.IntegrationTesting is not affiliated with Particular Software and is not officially supported by Particular Software.

> [!NOTE]
> Version 3 is a major rewrite with a new out-of-process architecture. Pre-release packages are available via the [Feedz.io pre-releases feed](https://f.feedz.io/mauroservienti/pre-releases/nuget/index.json).

---

## Prerequisites

- [Docker](https://www.docker.com/products/docker-desktop/) installed and running — all endpoints and infrastructure run in containers
- .NET 8 or .NET 10 SDK

## Installation

Install the core test-host package into your test project:

```
dotnet add package NServiceBus.IntegrationTesting
```

Add infrastructure extension packages for the transports and persistence your endpoints use:

```
dotnet add package NServiceBus.IntegrationTesting.RabbitMQ     # RabbitMQ transport
dotnet add package NServiceBus.IntegrationTesting.PostgreSql   # PostgreSQL persistence or transport
dotnet add package NServiceBus.IntegrationTesting.MySql        # MySQL persistence
dotnet add package NServiceBus.IntegrationTesting.SqlServer    # SQL Server persistence or transport
dotnet add package NServiceBus.IntegrationTesting.MongoDb      # MongoDB persistence
dotnet add package NServiceBus.IntegrationTesting.RavenDb      # RavenDB persistence
```

Install the agent package into each `*.Testing` companion project, matching the NServiceBus version that endpoint uses:

| NServiceBus version | Agent package | Target framework |
|---|---|---|
| 10 | `NServiceBus.IntegrationTesting.AgentV10` | net10.0 |
| 9 | `NServiceBus.IntegrationTesting.AgentV9` | net8.0 |
| 8 | `NServiceBus.IntegrationTesting.AgentV8` | net6.0 |

```
dotnet add package NServiceBus.IntegrationTesting.AgentV10   # for NServiceBus 10 endpoints
dotnet add package NServiceBus.IntegrationTesting.AgentV9    # for NServiceBus 9 endpoints
dotnet add package NServiceBus.IntegrationTesting.AgentV8    # for NServiceBus 8 endpoints
```

---

## Architecture: out-of-process with Docker containers

Each endpoint runs in its own Docker container. Endpoints can run **different NServiceBus major versions** side-by-side. The test process acts as a gRPC server; each endpoint embeds a lightweight gRPC agent that connects home on startup.

### How it works

```
Test process
└─ TestHostServer (gRPC, dynamic port)
   ├─[bidirectional streaming]─► SampleEndpoint container (NSB 10 / .NET 10)
   │                                └─► RabbitMQ container
   │                                └─► PostgreSQL container
   └─[bidirectional streaming]─► AnotherEndpoint container (NSB 9 / .NET 8)
                                     └─► RabbitMQ container
```

### Writing a test

<!-- snippet: writing-a-test -->
<a id='snippet-writing-a-test'></a>
```cs
[TestFixture]
public class WhenSomeMessageIsSent
{
    static TestEnvironment _env = null!;

    [OneTimeSetUp]
    public static async Task SetUp()
    {
        var srcDir = Path.Combine(FindRepoRoot(), "src");

        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMQ()
            .UsePostgreSql()
            .AddEndpoint("SampleEndpoint", "SampleEndpoint.Testing/Dockerfile")
            .AddEndpoint("AnotherEndpoint", "AnotherEndpoint.Testing/Dockerfile")
            .StartAsync();
    }

    [OneTimeTearDown]
    public static Task TearDown() => _env.DisposeAsync().AsTask();

    [Test]
    public async Task The_full_chain_should_be_processed()
    {
        var correlationId = await _env.GetEndpoint("SampleEndpoint")
            .ExecuteScenarioAsync("SomeMessage", new Dictionary<string, string>
            {
                { "ID", Guid.NewGuid().ToString() }
            });

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
        var correlationId = await _env.GetEndpoint("SampleEndpoint")
            .ExecuteScenarioAsync("FailingMessage", new Dictionary<string, string>
            {
                { "ID", Guid.NewGuid().ToString() }
            });

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
            ?? throw new InvalidOperationException(
                "Cannot locate repository root. Ensure the test runs inside a git repository.");
    }
}
```
<sup><a href='/src/Snippets/TestFixtureSnippets.cs#L6-L85' title='Snippet source file'>snippet source</a> | <a href='#snippet-writing-a-test' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

> [!NOTE]
> The example uses **NUnit** (`[TestFixture]`, `[OneTimeSetUp]`, `Assert.Multiple`). xUnit or MSTest users need to adapt the fixture lifecycle and assertion calls accordingly.

`FindRepoRoot()` is a helper you write once in your test project. It walks up from the test output directory until it finds the `.git` folder, giving `TestEnvironmentBuilder` a stable base path for Dockerfile locations regardless of where `dotnet test` is invoked from.

<!-- Intentionally not a relative link, since this README is also included in the NuGet package -->
For a full walkthrough and API reference, see the **[documentation](https://github.com/mauroservienti/NServiceBus.IntegrationTesting/blob/master/docs/README.md)** or the **[getting started guide](https://github.com/mauroservienti/NServiceBus.IntegrationTesting/blob/master/docs/getting-started.md)**.

### Key concepts

**`TestEnvironmentBuilder`** — fluent builder that starts a Docker network, infrastructure containers, the gRPC test host, and all endpoint containers. Optionally adds [WireMock.Net](https://github.com/WireMock-Net/WireMock.Net) for HTTP stubbing via `.UseWireMock()`. It automatically injects `NSBUS_TESTING_HOST` into every endpoint container so the agent knows where to connect — endpoints do not configure this themselves.

**Scenarios** — named entry points defined in the `*.Testing` companion project. A scenario runs _inside the endpoint process_, using the real `IMessageSession`, so no cross-process message serialization occurs:

<!-- snippet: scenario -->
<a id='snippet-scenario'></a>
```cs
public class SomeMessageScenario : Scenario
{
    public override string Name => "SomeMessage";

    public override async Task Execute(IMessageSession session,
        Dictionary<string, string> args, CancellationToken ct)
        => await session.Send(new SomeMessage { Id = Guid.Parse(args["ID"]) });
}
```
<sup><a href='/src/Snippets/ScenarioSnippets.cs#L7-L16' title='Snippet source file'>snippet source</a> | <a href='#snippet-scenario' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**`ObserveContext`** — fluent API for waiting on events correlated by scenario invocation. Conditions: `.HandlerInvoked`, `.SagaInvoked`, `.MessageDispatched`, `.MessageFailed`. Each condition type supports no-arg, single-event predicate, and list predicate overloads.

**Production / testing separation** — production endpoints have zero testing dependencies. Each endpoint has a companion `*.Testing` project that wraps the production config with `IntegrationTestingBootstrap` and registers scenarios.

### Why `*.Testing` companion projects?

The production endpoint (`SampleEndpoint/`) has no reference to any testing library. It stays clean and deployable as-is.

The `*.Testing` companion project exists solely for integration tests. It:

1. **Wraps the production config** — calls the same `Config.Create()` factory used in
   production, then overlays test-specific settings (e.g. shorter retry counts so failing
   messages reach the error queue quickly instead of spending minutes in retry loops).
2. **Registers scenarios** — named entry points that run _inside_ the endpoint process
   using the real `IMessageSession`. Because scenarios execute in-process, messages are
   created and sent natively — no cross-process serialization, no test-only message
   constructors, no leaking of test concerns into production types.
3. **Provides the Dockerfile** — builds a container image from the companion project, not
   from the production project. Only the testing image carries the agent and scenario
   registrations. A minimal `Dockerfile` (build context: `src/`) looks like:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY YourMessages/YourMessages.csproj YourMessages/
COPY YourEndpoint/YourEndpoint.csproj YourEndpoint/
COPY YourEndpoint.Testing/YourEndpoint.Testing.csproj YourEndpoint.Testing/
RUN dotnet restore YourEndpoint.Testing/YourEndpoint.Testing.csproj

COPY YourMessages/ YourMessages/
COPY YourEndpoint/ YourEndpoint/
COPY YourEndpoint.Testing/ YourEndpoint.Testing/
RUN dotnet publish YourEndpoint.Testing/YourEndpoint.Testing.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "YourEndpoint.Testing.dll"]
```

```text
SampleEndpoint/                  ← production code, zero test dependencies
SampleEndpoint.Testing/          ← wraps production config, adds agent + scenarios
  Program.cs                     ← IntegrationTestingBootstrap.RunAsync(...)
  SomeMessageScenario.cs         ← implements Scenario base class
  Dockerfile                     ← builds the testable container image
SampleEndpoint.Tests/            ← NUnit test project, references Testing only for
  WhenSomeMessageIsSent.cs         compile-time validation (ReferenceOutputAssembly=false)
```

The test project references the `*.Testing` project with `ReferenceOutputAssembly="false"`. This gives compile-time validation that the `*.Testing` project builds, while at test runtime the container image is built from its Dockerfile — there is no in-process loading of the endpoint assembly.

## NuGet packages

| Package | Purpose |
|---|---|
| `NServiceBus.IntegrationTesting` | Test host, `TestEnvironmentBuilder`, observe API |
| `NServiceBus.IntegrationTesting.RabbitMQ` | RabbitMQ transport container |
| `NServiceBus.IntegrationTesting.PostgreSql` | PostgreSQL persistence container |
| `NServiceBus.IntegrationTesting.MySql` | MySQL persistence container |
| `NServiceBus.IntegrationTesting.SqlServer` | SQL Server persistence container |
| `NServiceBus.IntegrationTesting.MongoDb` | MongoDB persistence container |
| `NServiceBus.IntegrationTesting.RavenDb` | RavenDB persistence container |
| `NServiceBus.IntegrationTesting.AgentV10` | Agent for NServiceBus 10 (net10.0) |
| `NServiceBus.IntegrationTesting.AgentV9` | Agent for NServiceBus 9 (net8.0) |
| `NServiceBus.IntegrationTesting.AgentV8` | Agent for NServiceBus 8 (net6.0) |

- [NuGet stable releases](https://www.nuget.org/packages/NServiceBus.IntegrationTesting)
- [Feedz.io pre-releases feed](https://f.feedz.io/mauroservienti/pre-releases/nuget/index.json)

---

Icon [test](https://thenounproject.com/search/?q=test&i=2829166) by Andrei Yushchenko from the Noun Project
