# Test Isolation

Integration tests that share a long-lived `TestEnvironment` run faster than tests that
spin up fresh containers for every test. The trade-off is that state from one test
can leak into the next — in-memory endpoint state, saga rows, application tables, or
queued messages.

This page covers two strategies for resetting that state between tests, with examples
for NUnit, xUnit, and MSTest.

## Strategies

### Option A — Restart the endpoint container

`RestartEndpointAsync` stops the endpoint container, starts it again, and waits for
the agent to reconnect. This:

- Clears all **in-memory state** inside the endpoint (deferred pipeline, caches, etc.)
- Establishes a fresh gRPC connection between the agent and the test host
- Does **not** clear database-persisted state (saga rows, application tables)

Use this when the endpoint holds meaningful in-memory state between tests, or when
you want the simplest possible reset with no knowledge of the database schema.

### Option B — Truncate tables

Running a SQL command inside the database container via `GetInfrastructure(...).ExecAsync(...)`
truncates saga and application tables in milliseconds — far cheaper than restarting a
container. This:

- Clears **persisted saga and application data**
- Does **not** reset in-memory endpoint state

Use this when tests produce and consume messages quickly, unique correlation IDs per
test prevent cross-test observation bleed, and the per-test overhead of a container
restart is too high.

> Both strategies can be combined: restart the endpoint for a clean in-memory slate and
> truncate tables for a clean persistence slate.

## NUnit

### Restart the endpoint

Set up the environment once in `[OneTimeSetUp]`, restart the endpoint in `[SetUp]`
before each test, and tear down in `[OneTimeTearDown]`.

<!-- snippet: isolation-nunit-restart-endpoint -->
<a id='snippet-isolation-nunit-restart-endpoint'></a>
```cs
[NUnitFixture]
[NonParallelizable]
public class WhenSomeCommandIsSent_NUnit_Restart
{
    static TestEnvironment _env = null!;
    static EndpointHandle _yourEndpoint = null!;

    [NUnitSetUp]
    public static async Task SetUp()
    {
        string srcDir = null!;
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMQ()
            .UsePostgreSql()
            .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
            .StartAsync();

        _yourEndpoint = _env.GetEndpoint("YourEndpoint");
    }

    [NUnitTearDown]
    public static Task TearDown() => _env.DisposeAsync().AsTask();

    // Restart the endpoint before each test — clears all in-memory state and
    // re-establishes a fresh agent connection.
    [NUnitBefore]
    public Task ResetEndpoint() => _env.RestartEndpointAsync("YourEndpoint");

    [NUnitTest]
    public async Task Handler_should_be_invoked()
    {
        var correlationId = await _yourEndpoint.ExecuteScenarioAsync(
            "SomeCommand Scenario",
            new Dictionary<string, string> { { "ID", Guid.NewGuid().ToString() } });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var results = await _env.Observe(correlationId, cts.Token)
            .HandlerInvoked("SomeMessageHandler")
            .WhenAllAsync();

        NUnitAssert.That(
            results.HandlerInvoked("SomeMessageHandler").EndpointName,
            Is.EqualTo("YourEndpoint"));
    }
}
```
<sup><a href='/src/Snippets/TestIsolationSnippets.cs#L27-L74' title='Snippet source file'>snippet source</a> | <a href='#snippet-isolation-nunit-restart-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Reset database state

<!-- snippet: isolation-nunit-reset-db -->
<a id='snippet-isolation-nunit-reset-db'></a>
```cs
[NUnitFixture]
[NonParallelizable]
public class WhenSomeCommandIsSent_NUnit_ResetDb
{
    static TestEnvironment _env = null!;
    static EndpointHandle _yourEndpoint = null!;

    [NUnitSetUp]
    public static async Task SetUp()
    {
        string srcDir = null!;
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMQ()
            .UsePostgreSql()
            .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
            .StartAsync();

        _yourEndpoint = _env.GetEndpoint("YourEndpoint");
    }

    [NUnitTearDown]
    public static Task TearDown() => _env.DisposeAsync().AsTask();

    // Truncate saga and application tables before each test — much faster than
    // restarting the endpoint. Use unique correlation IDs per test to avoid
    // cross-test observation bleed.
    [NUnitBefore]
    public async Task ResetDatabaseState()
    {
        var result = await _env.GetInfrastructure(PostgreSqlContainerOptions.InfrastructureKey)
            .ExecAsync(["psql", "-U", "postgres", "-c", "TRUNCATE orders, saga_data"]);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Database reset failed: {result.Stderr}");
    }

    [NUnitTest]
    public async Task Handler_should_be_invoked()
    {
        var correlationId = await _yourEndpoint.ExecuteScenarioAsync(
            "SomeCommand Scenario",
            new Dictionary<string, string> { { "ID", Guid.NewGuid().ToString() } });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var results = await _env.Observe(correlationId, cts.Token)
            .HandlerInvoked("SomeMessageHandler")
            .WhenAllAsync();

        NUnitAssert.That(
            results.HandlerInvoked("SomeMessageHandler").EndpointName,
            Is.EqualTo("YourEndpoint"));
    }
}
```
<sup><a href='/src/Snippets/TestIsolationSnippets.cs#L76-L131' title='Snippet source file'>snippet source</a> | <a href='#snippet-isolation-nunit-reset-db' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## xUnit

xUnit uses a shared `IClassFixture<T>` to hold the long-lived environment. An
`IAsyncLifetime` on the test class provides per-test setup and teardown.

### Shared fixture

Declare the fixture once and reuse it across all test classes in the collection:

<!-- snippet: isolation-xunit-fixture -->
<a id='snippet-isolation-xunit-fixture'></a>
```cs
public class IntegrationTestFixture : IAsyncLifetime
{
    public TestEnvironment Env { get; private set; } = null!;
    public EndpointHandle YourEndpoint { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        string srcDir = null!;
        Env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMQ()
            .UsePostgreSql()
            .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
            .StartAsync();

        YourEndpoint = Env.GetEndpoint("YourEndpoint");
    }

    public Task DisposeAsync() => Env.DisposeAsync().AsTask();
}
```
<sup><a href='/src/Snippets/TestIsolationSnippets.cs#L135-L156' title='Snippet source file'>snippet source</a> | <a href='#snippet-isolation-xunit-fixture' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Restart the endpoint

<!-- snippet: isolation-xunit-restart-endpoint -->
<a id='snippet-isolation-xunit-restart-endpoint'></a>
```cs
[Collection("Integration")]
public class WhenSomeCommandIsSent_xUnit_Restart
    : IClassFixture<IntegrationTestFixture>, IAsyncLifetime
{
    readonly IntegrationTestFixture _fixture;

    public WhenSomeCommandIsSent_xUnit_Restart(IntegrationTestFixture fixture)
        => _fixture = fixture;

    // IAsyncLifetime.InitializeAsync runs before each test — restart the
    // endpoint to clear in-memory state and get a fresh agent connection.
    public Task InitializeAsync()
        => _fixture.Env.RestartEndpointAsync("YourEndpoint");

    public Task DisposeAsync() => Task.CompletedTask;

    [XFact]
    public async Task Handler_should_be_invoked()
    {
        var correlationId = await _fixture.YourEndpoint.ExecuteScenarioAsync(
            "SomeCommand Scenario",
            new Dictionary<string, string> { { "ID", Guid.NewGuid().ToString() } });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var results = await _fixture.Env.Observe(correlationId, cts.Token)
            .HandlerInvoked("SomeMessageHandler")
            .WhenAllAsync();

        Xunit.Assert.Equal("YourEndpoint",
            results.HandlerInvoked("SomeMessageHandler").EndpointName);
    }
}
```
<sup><a href='/src/Snippets/TestIsolationSnippets.cs#L158-L191' title='Snippet source file'>snippet source</a> | <a href='#snippet-isolation-xunit-restart-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Reset database state

<!-- snippet: isolation-xunit-reset-db -->
<a id='snippet-isolation-xunit-reset-db'></a>
```cs
[Collection("Integration")]
public class WhenSomeCommandIsSent_xUnit_ResetDb
    : IClassFixture<IntegrationTestFixture>, IAsyncLifetime
{
    readonly IntegrationTestFixture _fixture;

    public WhenSomeCommandIsSent_xUnit_ResetDb(IntegrationTestFixture fixture)
        => _fixture = fixture;

    public async Task InitializeAsync()
    {
        var result = await _fixture.Env
            .GetInfrastructure(PostgreSqlContainerOptions.InfrastructureKey)
            .ExecAsync(["psql", "-U", "postgres", "-c", "TRUNCATE orders, saga_data"]);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Database reset failed: {result.Stderr}");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [XFact]
    public async Task Handler_should_be_invoked()
    {
        var correlationId = await _fixture.YourEndpoint.ExecuteScenarioAsync(
            "SomeCommand Scenario",
            new Dictionary<string, string> { { "ID", Guid.NewGuid().ToString() } });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var results = await _fixture.Env.Observe(correlationId, cts.Token)
            .HandlerInvoked("SomeMessageHandler")
            .WhenAllAsync();

        Xunit.Assert.Equal("YourEndpoint",
            results.HandlerInvoked("SomeMessageHandler").EndpointName);
    }
}
```
<sup><a href='/src/Snippets/TestIsolationSnippets.cs#L193-L231' title='Snippet source file'>snippet source</a> | <a href='#snippet-isolation-xunit-reset-db' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## MSTest

### Restart the endpoint

Use `[ClassInitialize]` / `[ClassCleanup]` for once-per-class lifecycle and
`[TestInitialize]` for per-test reset:

<!-- snippet: isolation-mstest-restart-endpoint -->
<a id='snippet-isolation-mstest-restart-endpoint'></a>
```cs
[MSTestClass]
public class WhenSomeCommandIsSent_MSTest_Restart
{
    static TestEnvironment _env = null!;
    static EndpointHandle _yourEndpoint = null!;

    [MSTestInit]
    public static async Task SetUp()
    {
        string srcDir = null!;
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMQ()
            .UsePostgreSql()
            .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
            .StartAsync();

        _yourEndpoint = _env.GetEndpoint("YourEndpoint");
    }

    [MSTestCleanup]
    public static Task TearDown() => _env.DisposeAsync().AsTask();

    [MSTestBefore]
    public Task ResetEndpoint() => _env.RestartEndpointAsync("YourEndpoint");

    [MSTestMethod]
    public async Task Handler_should_be_invoked()
    {
        var correlationId = await _yourEndpoint.ExecuteScenarioAsync(
            "SomeCommand Scenario",
            new Dictionary<string, string> { { "ID", Guid.NewGuid().ToString() } });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var results = await _env.Observe(correlationId, cts.Token)
            .HandlerInvoked("SomeMessageHandler")
            .WhenAllAsync();

        MSTestAssert.AreEqual(
            "YourEndpoint",
            results.HandlerInvoked("SomeMessageHandler").EndpointName);
    }
}
```
<sup><a href='/src/Snippets/TestIsolationSnippets.cs#L235-L279' title='Snippet source file'>snippet source</a> | <a href='#snippet-isolation-mstest-restart-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Reset database state

<!-- snippet: isolation-mstest-reset-db -->
<a id='snippet-isolation-mstest-reset-db'></a>
```cs
[MSTestClass]
public class WhenSomeCommandIsSent_MSTest_ResetDb
{
    static TestEnvironment _env = null!;
    static EndpointHandle _yourEndpoint = null!;

    [MSTestInit]
    public static async Task SetUp()
    {
        string srcDir = null!;
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMQ()
            .UsePostgreSql()
            .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
            .StartAsync();

        _yourEndpoint = _env.GetEndpoint("YourEndpoint");
    }

    [MSTestCleanup]
    public static Task TearDown() => _env.DisposeAsync().AsTask();

    [MSTestBefore]
    public async Task ResetDatabaseState()
    {
        var result = await _env.GetInfrastructure(PostgreSqlContainerOptions.InfrastructureKey)
            .ExecAsync(["psql", "-U", "postgres", "-c", "TRUNCATE orders, saga_data"]);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Database reset failed: {result.Stderr}");
    }

    [MSTestMethod]
    public async Task Handler_should_be_invoked()
    {
        var correlationId = await _yourEndpoint.ExecuteScenarioAsync(
            "SomeCommand Scenario",
            new Dictionary<string, string> { { "ID", Guid.NewGuid().ToString() } });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var results = await _env.Observe(correlationId, cts.Token)
            .HandlerInvoked("SomeMessageHandler")
            .WhenAllAsync();

        MSTestAssert.AreEqual(
            "YourEndpoint",
            results.HandlerInvoked("SomeMessageHandler").EndpointName);
    }
}
```
<sup><a href='/src/Snippets/TestIsolationSnippets.cs#L281-L332' title='Snippet source file'>snippet source</a> | <a href='#snippet-isolation-mstest-reset-db' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## What restart clears vs. what it does not

| State | Endpoint restart | Table truncation |
|---|---|---|
| In-memory endpoint caches | Cleared | Not cleared |
| NServiceBus deferred pipeline | Cleared | Not cleared |
| gRPC agent connection | Re-established | Not affected |
| Persisted saga rows | Not cleared | Cleared |
| Application tables | Not cleared | Cleared |
| RabbitMQ queued messages | Not cleared | Not cleared |

Queued messages that survive a restart are processed when the endpoint comes back up.
Use unique correlation IDs per test (e.g. `Guid.NewGuid()`) so that observations
targeting one test's correlation ID are not triggered by leftover messages from a
previous test.
