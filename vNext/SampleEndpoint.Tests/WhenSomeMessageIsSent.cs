using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using NServiceBus.IntegrationTesting.Containers;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace SampleEndpoint.Tests;

/// <summary>
/// Proves the full multi-endpoint gRPC agent channel works end-to-end
/// with both endpoints running as Docker containers.
///
/// Flow:
///   1. A shared Docker network is created.
///   2. RabbitMQ starts in a container on that network (alias "rabbitmq").
///   3. PostgreSQL starts in a container on that network (alias "postgres").
///   4. The gRPC test host server starts in-process, bound to 0.0.0.0 on a dynamic port.
///   5. Docker images for SampleEndpoint.Testing and AnotherEndpoint.Testing are built.
///   6. Both endpoints start as containers on the shared network.
///      Agents dial NSBUS_TESTING_HOST = host.docker.internal:{port}.
///   7. Test waits for both agents to connect.
///   8. Test executes the "SomeMessage" scenario on SampleEndpoint:
///      a. SomeMessageHandler handles SomeMessage → sends AnotherMessage to AnotherEndpoint.
///      b. AnotherMessageHandler handles AnotherMessage → replies with SomeReply.
///      c. SomeReplyHandler and SomeReplySaga handle SomeReply.
///      d. SomeReplySaga sets a 20s timeout; on firing sends SagaCompletedMessage.
///      e. SagaCompletedMessageHandler handles SagaCompletedMessage — test done condition.
/// </summary>
[TestFixture]
public class WhenSomeMessageIsSent
{
    static INetwork _network = null!;
    static RabbitMqContainer _rabbitMq = null!;
    static PostgreSqlContainer _postgreSql = null!;
    static TestHostServer _testHost = null!;
    static EndpointHandle _sampleEndpoint = null!;
    static EndpointHandle _anotherEndpoint = null!;
    static IContainer _sampleEndpointContainer = null!;
    static IContainer _anotherEndpointContainer = null!;

    [OneTimeSetUp]
    public static async Task SetUp()
    {
        // ── Step 1: Shared Docker network ───────────────────────────────────
        _network = new NetworkBuilder().Build();
        await _network.CreateAsync();

        // ── Step 2: RabbitMQ ────────────────────────────────────────────────
        _rabbitMq = new RabbitMqBuilder("rabbitmq:management")
            .WithNetwork(_network)
            .WithNetworkAliases("rabbitmq")
            .Build();

        // ── Step 3: PostgreSQL ───────────────────────────────────────────────
        _postgreSql = new PostgreSqlBuilder("postgres:15.1")
            .WithNetwork(_network)
            .WithNetworkAliases("postgres")
            .Build();

        await Task.WhenAll(_rabbitMq.StartAsync(), _postgreSql.StartAsync());

        // ── Step 4: gRPC test host ───────────────────────────────────────────
        _testHost = new TestHostServer();
        await _testHost.StartAsync();

        _sampleEndpoint = _testHost.GetEndpoint("SampleEndpoint");
        _anotherEndpoint = _testHost.GetEndpoint("AnotherEndpoint");

        // ── Step 5: Build Docker images ──────────────────────────────────────
        var repoRoot = FindRepoRoot();
        var vNextDir = Path.Combine(repoRoot, "vNext");

        var sampleImage = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(vNextDir)
            .WithDockerfile("SampleEndpoint.Testing/Dockerfile")
            .Build();

        var anotherImage = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(vNextDir)
            .WithDockerfile("AnotherEndpoint.Testing/Dockerfile")
            .Build();

        await Task.WhenAll(sampleImage.CreateAsync(), anotherImage.CreateAsync());

        // ── Step 6: Start endpoint containers ───────────────────────────────
        // SampleEndpoint needs both RabbitMQ and PostgreSQL connection strings.
        var sampleEnvVars = new Dictionary<string, string>
        {
            ["NSBUS_TESTING_HOST"] = _testHost.ContainerAddress,
            ["RABBITMQ_CONNECTION_STRING"] =
                $"host=rabbitmq;username={RabbitMqBuilder.DefaultUsername};password={RabbitMqBuilder.DefaultPassword}",
            ["POSTGRESQL_CONNECTION_STRING"] =
                $"Host=postgres;Port=5432;Database={PostgreSqlBuilder.DefaultDatabase};Username={PostgreSqlBuilder.DefaultUsername};Password={PostgreSqlBuilder.DefaultPassword}"
        };

        // AnotherEndpoint has no persistence, so no PostgreSQL needed.
        var anotherEnvVars = new Dictionary<string, string>
        {
            ["NSBUS_TESTING_HOST"] = _testHost.ContainerAddress,
            ["RABBITMQ_CONNECTION_STRING"] =
                $"host=rabbitmq;username={RabbitMqBuilder.DefaultUsername};password={RabbitMqBuilder.DefaultPassword}"
        };

        _sampleEndpointContainer = new ContainerBuilder(sampleImage.FullName)
            .WithNetwork(_network)
            .WithEnvironment(sampleEnvVars)
            .Build();

        _anotherEndpointContainer = new ContainerBuilder(anotherImage.FullName)
            .WithNetwork(_network)
            .WithEnvironment(anotherEnvVars)
            .Build();

        await Task.WhenAll(
            _sampleEndpointContainer.StartAsync(),
            _anotherEndpointContainer.StartAsync());

        // ── Step 7: Wait for both agents to connect ──────────────────────────
        using var agentWaitCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await Task.WhenAll(
            _sampleEndpoint.WaitForConnectedAsync(agentWaitCts.Token),
            _anotherEndpoint.WaitForConnectedAsync(agentWaitCts.Token));
    }

    [TearDown]
    public async Task DumpContainerLogsOnFailure()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Failed)
            return;

        var (sampleStdout, sampleStderr) = await _sampleEndpointContainer.GetLogsAsync();
        TestContext.Out.WriteLine("=== SampleEndpoint container stdout ===");
        TestContext.Out.WriteLine(sampleStdout);
        TestContext.Out.WriteLine("=== SampleEndpoint container stderr ===");
        TestContext.Out.WriteLine(sampleStderr);

        var (anotherStdout, anotherStderr) = await _anotherEndpointContainer.GetLogsAsync();
        TestContext.Out.WriteLine("=== AnotherEndpoint container stdout ===");
        TestContext.Out.WriteLine(anotherStdout);
        TestContext.Out.WriteLine("=== AnotherEndpoint container stderr ===");
        TestContext.Out.WriteLine(anotherStderr);
    }

    [OneTimeTearDown]
    public static async Task TearDown()
    {
        await Task.WhenAll(
            _sampleEndpointContainer.StopAsync(),
            _anotherEndpointContainer.StopAsync());

        await Task.WhenAll(
            _sampleEndpointContainer.DisposeAsync().AsTask(),
            _anotherEndpointContainer.DisposeAsync().AsTask());

        await _testHost.DisposeAsync();
        await _rabbitMq.DisposeAsync();
        await _postgreSql.DisposeAsync();
        await _network.DeleteAsync();
    }

    [Test]
    public async Task The_full_chain_should_be_processed()
    {
        var args = new Dictionary<string, string>
        {
            { "ID", Guid.NewGuid().ToString() }
        };
        var correlationId = await _sampleEndpoint.ExecuteScenarioAsync("SomeMessage", args);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var results = await _testHost.Observe(correlationId, cts.Token)
            .HandlerInvoked("SomeMessageHandler")
            .HandlerInvoked("AnotherMessageHandler")
            .HandlerInvoked("SomeReplyHandler")
            .WhenAllAsync();

        Assert.Multiple(() =>
        {
            Assert.That(results.HandlerInvoked("SomeMessageHandler").EndpointName, Is.EqualTo("SampleEndpoint"));
            Assert.That(results.HandlerInvoked("AnotherMessageHandler").EndpointName, Is.EqualTo("AnotherEndpoint"));
            Assert.That(results.HandlerInvoked("SomeReplyHandler").EndpointName, Is.EqualTo("SampleEndpoint"));
        });
    }

    [Test]
    public async Task SampleEndpoint_should_dispatch_AnotherMessage()
    {
        var args = new Dictionary<string, string>
        {
            { "ID", Guid.NewGuid().ToString() }
        };
        var correlationId = await _sampleEndpoint.ExecuteScenarioAsync("SomeMessage", args);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var results = await _testHost.Observe(correlationId, cts.Token)
            .MessageDispatched("AnotherMessage")
            .WhenAllAsync();

        var dispatched = results.MessageDispatched("AnotherMessage");
        Assert.Multiple(() =>
        {
            Assert.That(dispatched.EndpointName, Is.EqualTo("SampleEndpoint"));
            Assert.That(dispatched.Intent, Is.EqualTo("Send"));
        });
    }

    [Test]
    public async Task The_saga_should_complete_after_timeout()
    {
        var args = new Dictionary<string, string>
        {
            { "ID", Guid.NewGuid().ToString() }
        };
        var correlationId = await _sampleEndpoint.ExecuteScenarioAsync("SomeMessage", args);

        // The saga starts on SomeReply and sets a 20s timeout — allow enough headroom.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var results = await _testHost.Observe(correlationId, cts.Token)
            .HandlerInvoked("SomeReplySaga")
            // Validates that RequestTimeout stamped the correlation ID at IOutgoingLogicalMessageContext.
            .MessageDispatched("SomeReplySagaTimeout")
            .HandlerInvoked("SagaCompletedMessageHandler")
            .WhenAllAsync();

        Assert.Multiple(() =>
        {
            var sagaStart = results.HandlerInvoked("SomeReplySaga");
            Assert.That(sagaStart.EndpointName, Is.EqualTo("SampleEndpoint"));
            Assert.That(sagaStart.IsSaga, Is.True);
            Assert.That(sagaStart.SagaIsNew, Is.True);

            var timeoutDispatched = results.MessageDispatched("SomeReplySagaTimeout");
            Assert.That(timeoutDispatched.EndpointName, Is.EqualTo("SampleEndpoint"));
            Assert.That(timeoutDispatched.Intent, Is.EqualTo("RequestTimeout"));

            Assert.That(results.HandlerInvoked("SagaCompletedMessageHandler").EndpointName, Is.EqualTo("SampleEndpoint"));
        });
    }

    /// <summary>
    /// Exercises the single-event predicate overload (Func&lt;TEvent, bool&gt;).
    /// The done condition is expressed as a property check on the latest event,
    /// so the test completes as soon as the saga starts AND is flagged as new —
    /// encoding the business assertion directly in the done condition.
    /// </summary>
    [Test]
    public async Task Single_event_predicate_fires_when_saga_starts_as_new()
    {
        var args = new Dictionary<string, string>
        {
            { "ID", Guid.NewGuid().ToString() }
        };
        var correlationId = await _sampleEndpoint.ExecuteScenarioAsync("SomeMessage", args);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // The predicate on the handler encodes "saga must be new" as the done condition,
        // not just "saga was invoked once".  The predicate on the dispatch checks intent.
        var results = await _testHost.Observe(correlationId, cts.Token)
            .HandlerInvoked("SomeReplySaga", evt => evt.IsSaga && evt.SagaIsNew)
            .MessageDispatched("SomeReplySagaTimeout", evt => evt.Intent == "RequestTimeout")
            .WhenAllAsync();

        Assert.Multiple(() =>
        {
            Assert.That(results.HandlerInvoked("SomeReplySaga").EndpointName, Is.EqualTo("SampleEndpoint"));
            Assert.That(results.MessageDispatched("SomeReplySagaTimeout").EndpointName, Is.EqualTo("SampleEndpoint"));
        });
    }

    /// <summary>
    /// Exercises the list predicate overload (Func&lt;IReadOnlyList&lt;TEvent&gt;, bool&gt;).
    /// Uses the accumulated list to assert that ALL collected dispatches carry the
    /// expected intent — a condition that cannot be expressed with a single-event predicate.
    /// </summary>
    [Test]
    public async Task List_predicate_fires_when_all_collected_dispatches_have_expected_intent()
    {
        var args = new Dictionary<string, string>
        {
            { "ID", Guid.NewGuid().ToString() }
        };
        var correlationId = await _sampleEndpoint.ExecuteScenarioAsync("SomeMessage", args);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Wait until we have at least one SomeReplySagaTimeout dispatch AND
        // every collected dispatch has Intent == "RequestTimeout".
        var results = await _testHost.Observe(correlationId, cts.Token)
            .MessageDispatched("SomeReplySagaTimeout",
                all => all.Count >= 1 && all.All(e => e.Intent == "RequestTimeout"))
            .WhenAllAsync();

        var dispatches = results.MessageDispatches("SomeReplySagaTimeout");
        Assert.Multiple(() =>
        {
            Assert.That(dispatches, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(dispatches.Select(e => e.Intent), Is.All.EqualTo("RequestTimeout"));
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

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
