using NServiceBus.IntegrationTesting;
using NUnit.Framework;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace SampleEndpoint.Tests;

/// <summary>
/// Demonstrates how to stub an external HTTP dependency with WireMock.Net.
///
/// SomeMessageHandler reads WIREMOCK_URL from its environment and calls
/// {WIREMOCK_URL}/api/greeting before forwarding the message. The test:
///   1. Configures the stub before triggering the scenario.
///   2. Waits for SomeMessageHandler to be invoked (proves the full path ran).
///   3. Asserts WireMock received exactly the expected request.
/// </summary>
[TestFixture]
[NonParallelizable]
public class WhenSomeMessageIsSentWithWireMock
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
            .UseWireMock()
            .AddEndpoint("SampleEndpoint", "SampleEndpoint.Testing/Dockerfile")
            .AddEndpoint("AnotherEndpoint", "AnotherEndpoint.Testing/Dockerfile")
            .StartAsync();

        _sampleEndpoint = _env.GetEndpoint("SampleEndpoint");
    }

    [SetUp]
    public void LogTestName()
    {
        TestContext.Out.WriteLine($"=== Starting test: {TestContext.CurrentContext.Test.Name} ===");
    }

    [TearDown]
    public async Task DumpContainerLogsOnFailure()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Failed)
            return;

        var (sampleStdout, sampleStderr) = await _env.GetEndpointContainerLogsAsync("SampleEndpoint");
        TestContext.Out.WriteLine("=== SampleEndpoint container stdout ===");
        TestContext.Out.WriteLine(sampleStdout);
        TestContext.Out.WriteLine("=== SampleEndpoint container stderr ===");
        TestContext.Out.WriteLine(sampleStderr);

        var (anotherStdout, anotherStderr) = await _env.GetEndpointContainerLogsAsync("AnotherEndpoint");
        TestContext.Out.WriteLine("=== AnotherEndpoint container stdout ===");
        TestContext.Out.WriteLine(anotherStdout);
        TestContext.Out.WriteLine("=== AnotherEndpoint container stderr ===");
        TestContext.Out.WriteLine(anotherStderr);
    }

    [OneTimeTearDown]
    public static Task TearDown() => _env.DisposeAsync().AsTask();

    [Test]
    public async Task SomeMessageHandler_calls_external_service_and_WireMock_receives_the_request()
    {
        // Arrange — configure the stub before triggering the scenario so no request is missed.
        _env.WireMock!
            .Given(Request.Create().WithPath("/api/greeting").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("Hello from WireMock"));

        var args = new Dictionary<string, string>
        {
            { "ID", Guid.NewGuid().ToString() }
        };

        // Act
        var correlationId = await _sampleEndpoint.ExecuteScenarioAsync("SomeMessage", args);
        TestContext.Out.WriteLine($"Correlation ID: {correlationId}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        await _env.Observe(correlationId, cts.Token)
            .HandlerInvoked("SomeMessageHandler")
            .WhenAllAsync();

        // Assert — WireMock received the call the handler was supposed to make.
        Assert.That(
            _env.WireMock!.LogEntries.Any(e => e.RequestMessage.Path == "/api/greeting"),
            Is.True);
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
