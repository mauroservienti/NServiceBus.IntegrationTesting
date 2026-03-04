using NServiceBus.IntegrationTesting;
using NUnit.Framework;

namespace Snippets.GettingStartedTestFixture;

// begin-snippet: gs-test-fixture
[TestFixture]
[NonParallelizable]
public class WhenSomeCommandIsSent
{
    static TestEnvironment _env = null!;
    static EndpointHandle _yourEndpoint = null!;

    [OneTimeSetUp]
    public static async Task SetUp()
    {
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(TestEnvironmentBuilder.FindRootByDirectory(".git", "src"))
            .UseRabbitMQ()
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
            "SomeCommand Scenario",
            new Dictionary<string, string> { { "ID", Guid.NewGuid().ToString() } });

        // this is the overall test timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var results = await _env.Observe(correlationId, cts.Token)
            .HandlerInvoked("SomeMessageHandler")
            .WhenAllAsync();

        Assert.That(
            results.HandlerInvoked("SomeMessageHandler").EndpointName,
            Is.EqualTo("YourEndpoint"));
    }
}
// end-snippet
