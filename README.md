# NServiceBus.IntegrationTesting

![NServiceBus.IntegrationTesting icon](assets/icon.png)

NServiceBus.IntegrationTesting enables testing end-to-end business scenarios against real
production endpoints, real transports, and real persistence.

> **Disclaimer**: NServiceBus.IntegrationTesting is not affiliated with Particular Software and is not officially supported by Particular Software.

---

## Architecture: out-of-process with Docker containers

Each endpoint runs in its own Docker container. Endpoints can run **different NServiceBus major versions** side-by-side. The test process acts as a gRPC server; each endpoint embeds a lightweight gRPC agent that connects home on startup.

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
│ SampleEndpoint. │   │  AnotherEndpoint │
│ NSB 10 / .NET 10│   │  NSB 9 / .NET 9  │
│ container       │   │  container       │
└─────────────────┘   └──────────────────┘
        │                       │
    RabbitMQ container    PostgreSQL container
```

### Writing a test

<!-- snippet: vNext-writing-a-test -->
<a id='snippet-vNext-writing-a-test'></a>
```cs
[TestFixture]
public class WhenSomeMessageIsSent
{
    static TestEnvironment _env = null!;

    [OneTimeSetUp]
    public static async Task SetUp()
    {
        var vNextDir = Path.Combine(FindRepoRoot(), "vNext");

        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(vNextDir)
            .UseRabbitMq()
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
<sup><a href='/vNext/Snippets/TestFixtureSnippets.cs#L6-L85' title='Snippet source file'>snippet source</a> | <a href='#snippet-vNext-writing-a-test' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Key concepts

**`TestEnvironmentBuilder`** — fluent builder that starts a Docker network, infrastructure
containers, the gRPC test host, and all endpoint containers. Optionally adds
[WireMock.Net](https://github.com/WireMock-Net/WireMock.Net) for HTTP stubbing via
`.UseWireMock()`.

**Scenarios** — named entry points defined in the `*.Testing` companion project. A scenario
runs _inside the endpoint process_, using the real `IMessageSession`, so no cross-process
message serialization occurs:

<!-- snippet: vNext-scenario -->
<a id='snippet-vNext-scenario'></a>
```cs
public class SomeMessageScenario : Scenario
{
    public override string Name => "SomeMessage";

    public override async Task Execute(IMessageSession session,
        Dictionary<string, string> args, CancellationToken ct)
        => await session.Send(new SomeMessage { Id = Guid.Parse(args["ID"]) });
}
```
<sup><a href='/vNext/Snippets/ScenarioSnippets.cs#L7-L16' title='Snippet source file'>snippet source</a> | <a href='#snippet-vNext-scenario' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**`ObserveContext`** — fluent API for waiting on events correlated by scenario invocation.
Conditions: `.HandlerInvoked`, `.SagaInvoked`, `.MessageDispatched`, `.MessageFailed`.
Each condition type supports no-arg, single-event predicate, and list predicate overloads.

**Production / testing separation** — production endpoints have zero testing dependencies.
Each endpoint has a companion `*.Testing` project that wraps the production config with
`IntegrationTestingBootstrap` and registers scenarios.

### Why `*.Testing` companion projects?

The production endpoint (`SampleEndpoint/`) has no reference to any testing library. It
stays clean and deployable as-is.

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
   registrations.

```text
SampleEndpoint/                  ← production code, zero test dependencies
SampleEndpoint.Testing/          ← wraps production config, adds agent + scenarios
  Program.cs                     ← IntegrationTestingBootstrap.RunAsync(...)
  SomeMessageScenario.cs         ← implements Scenario base class
  Dockerfile                     ← builds the testable container image
SampleEndpoint.Tests/            ← NUnit test project, references Testing only for
  WhenSomeMessageIsSent.cs         compile-time validation (ReferenceOutputAssembly=false)
```

The test project references the `*.Testing` project with `ReferenceOutputAssembly="false"`.
This gives compile-time validation that the `*.Testing` project builds, while at test
runtime the container image is built from its Dockerfile — there is no in-process loading
of the endpoint assembly.

## V1.x: in-process with NServiceBus.AcceptanceTesting

> [!NOTE]
> v1.x of the framework has a fundamental limitation: all endpoints must share the same NServiceBus package(s) version because they run in-process.

The original framework enables tests like:

```csharp
[Test]
public async Task AReplyMessage_is_received_and_ASaga_is_started()
{
    var context = await Scenario.Define<IntegrationScenarioContext>()
        .WithEndpoint<MyServiceEndpoint>(b =>
            b.When(session => session.Send(new AMessage { AnIdentifier = Guid.NewGuid() })))
        .WithEndpoint<MyOtherServiceEndpoint>()
        .Done(c => c.SagaWasInvoked<ASaga>() || c.HasFailedMessages())
        .Run();

    var invokedSaga = context.InvokedSagas.Single(s => s.SagaType == typeof(ASaga));
    Assert.True(invokedSaga.IsNew);
}
```

### V1.x Resources

- [Exploring NServiceBus Integration Testing options](https://milestone.topics.it/2019/07/04/exploring-nservicebus-integration-testing-options.html)
- [NServiceBus.IntegrationTesting baby steps](https://milestone.topics.it/2021/04/07/nservicebus-integrationtesting-baby-steps.html)

---

Icon [test](https://thenounproject.com/search/?q=test&i=2829166) by Andrei Yushchenko from the Noun Project
