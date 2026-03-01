using NServiceBus.IntegrationTesting;
using NUnit.Framework;

namespace SampleEndpoint.Tests;

/// <summary>
/// Proves that a NServiceBus 8 endpoint (net8.0 / AgentV8) integrates cleanly with the
/// NServiceBus.IntegrationTesting framework alongside NServiceBus 9 and 10 endpoints.
///
/// Flow:
///   1. RabbitMQ starts in a container on a shared Docker network.
///   2. YetAnotherEndpoint.Testing image is built (NSB 8, net8.0).
///   3. The endpoint starts as a container; its agent connects to the gRPC test host.
///   4. A scenario sends YetAnotherMessage via SendLocal.
///   5. YetAnotherMessageHandler handles the message — test done condition.
/// </summary>
[TestFixture]
[NonParallelizable]
public class WhenYetAnotherMessageIsSent
{
    static TestEnvironment _env = null!;
    static EndpointHandle _yetAnotherEndpoint = null!;

    [OneTimeSetUp]
    public static async Task SetUp()
    {
        var srcDir = Path.Combine(FindRepoRoot(), "src");

        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMQ()
            .AddEndpoint("YetAnotherEndpoint", "YetAnotherEndpoint.Testing/Dockerfile")
            .StartAsync();

        _yetAnotherEndpoint = _env.GetEndpoint("YetAnotherEndpoint");
    }

    [TearDown]
    public async Task DumpContainerLogsOnFailure()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Failed)
            return;

        var (stdout, stderr) = await _env.GetEndpointContainerLogsAsync("YetAnotherEndpoint");
        TestContext.Out.WriteLine("=== YetAnotherEndpoint container stdout ===");
        TestContext.Out.WriteLine(stdout);
        TestContext.Out.WriteLine("=== YetAnotherEndpoint container stderr ===");
        TestContext.Out.WriteLine(stderr);
    }

    [OneTimeTearDown]
    public static Task TearDown() => _env.DisposeAsync().AsTask();

    [Test]
    public async Task Handler_should_be_invoked_on_nsb8_endpoint()
    {
        var correlationId = await _yetAnotherEndpoint.ExecuteScenarioAsync(
            "YetAnotherMessage",
            new Dictionary<string, string> { { "ID", Guid.NewGuid().ToString() } });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var results = await _env.Observe(correlationId, cts.Token)
            .HandlerInvoked("YetAnotherMessageHandler")
            .WhenAllAsync();

        Assert.That(
            results.HandlerInvoked("YetAnotherMessageHandler").EndpointName,
            Is.EqualTo("YetAnotherEndpoint"));
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
