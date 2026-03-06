extern alias MSTestFramework;

using DotNet.Testcontainers.Containers;
using NServiceBus.IntegrationTesting;
using NUnit.Framework;
using Xunit;

// Aliases to avoid NUnit/xUnit/MSTest attribute ambiguity in this file
using NUnitFixture  = NUnit.Framework.TestFixtureAttribute;
using NUnitSetUp    = NUnit.Framework.OneTimeSetUpAttribute;
using NUnitTearDown = NUnit.Framework.OneTimeTearDownAttribute;
using NUnitBefore   = NUnit.Framework.SetUpAttribute;
using NUnitTest     = NUnit.Framework.TestAttribute;
using NUnitAssert   = NUnit.Framework.Assert;
using MSTestClass   = MSTestFramework::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using MSTestInit    = MSTestFramework::Microsoft.VisualStudio.TestTools.UnitTesting.ClassInitializeAttribute;
using MSTestCleanup = MSTestFramework::Microsoft.VisualStudio.TestTools.UnitTesting.ClassCleanupAttribute;
using MSTestBefore  = MSTestFramework::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using MSTestMethod  = MSTestFramework::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using MSTestAssert  = MSTestFramework::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using XFact         = Xunit.FactAttribute;

namespace Snippets.TestIsolation;

// ── NUnit ────────────────────────────────────────────────────────────────────

// begin-snippet: isolation-nunit-restart-endpoint
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
// end-snippet

// begin-snippet: isolation-nunit-reset-db
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
// end-snippet

// ── xUnit ────────────────────────────────────────────────────────────────────

// begin-snippet: isolation-xunit-fixture
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
// end-snippet

// begin-snippet: isolation-xunit-restart-endpoint
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
// end-snippet

// begin-snippet: isolation-xunit-reset-db
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
// end-snippet

// ── MSTest ───────────────────────────────────────────────────────────────────

// begin-snippet: isolation-mstest-restart-endpoint
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
// end-snippet

// begin-snippet: isolation-mstest-reset-db
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
// end-snippet
