using NServiceBus.IntegrationTesting;
using NServiceBus.IntegrationTesting.Agent;
using NUnit.Framework;
using Snippets.GettingStarted;
using Snippets.GettingStartedConfig;
using Snippets.GettingStartedScenario;
using Snippets.GettingStartedSkip;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Snippets.GettingStartedAdvanced;

[TestFixture]
public class AdvancedSnippets
{
    static TestEnvironment _env = null!;
    static EndpointHandle _yourEndpoint = null!;

    public async Task MultiEndpoint()
    {
        string srcDir = null!;

        // begin-snippet: gs-multi-endpoint
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMQ()
            .UsePostgreSql()
            .AddEndpoint("OrdersEndpoint", "OrdersEndpoint.Testing/Dockerfile")
            .AddEndpoint("BillingEndpoint", "BillingEndpoint.Testing/Dockerfile")
            .AddEndpoint("ShippingEndpoint", "ShippingEndpoint.Testing/Dockerfile")
            .StartAsync();
        // end-snippet
    }

    public async Task TimeoutBootstrap()
    {
        // begin-snippet: gs-timeout-bootstrap
        // YourEndpoint.Testing/Program.cs
        await IntegrationTestingBootstrap.RunAsync(
            "YourEndpoint",
            YourEndpointConfig.Create,
            scenarios: [new SomeCommandScenario()],
            timeoutRules: [TimeoutRule.For<OrderProcessingTimeout>(TimeSpan.FromSeconds(5))]);
        // end-snippet
    }

    public async Task TimeoutAssertions()
    {
        string correlationId = null!;

        // begin-snippet: gs-timeout-assertions
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var results = await _env.Observe(correlationId, cts.Token)
            .SagaInvoked("OrderSaga")
            .MessageDispatched("OrderProcessingTimeout")
            .HandlerInvoked("TimeoutCompletionHandler")
            .WhenAllAsync();

        Assert.That(results.MessageDispatched("OrderProcessingTimeout").Intent,
            Is.EqualTo("RequestTimeout"));
        // end-snippet
    }

    public async Task SinglePredicate()
    {
        string correlationId = null!;
        using var cts = new CancellationTokenSource();

        // begin-snippet: gs-single-predicate
        // Only fires when the saga is new — no need to assert afterward
        var results = await _env.Observe(correlationId, cts.Token)
            .SagaInvoked("OrderSaga", evt => evt.SagaIsNew)
            .WhenAllAsync();
        // end-snippet
    }

    public async Task ListPredicate()
    {
        string correlationId = null!;
        using var cts = new CancellationTokenSource();

        // begin-snippet: gs-list-predicate
        // Wait until we have seen at least 3 dispatches of the same type
        var results = await _env.Observe(correlationId, cts.Token)
            .MessageDispatched("OrderStatusUpdated", all => all.Count >= 3)
            .WhenAllAsync();

        var dispatches = results.MessageDispatches("OrderStatusUpdated");
        Assert.That(dispatches, Has.Count.GreaterThanOrEqualTo(3));
        // end-snippet
    }

    public async Task WireMockSetup()
    {
        string srcDir = null!;

        // begin-snippet: gs-wiremock-setup
        // *.Tests.csproj: add <PackageReference Include="WireMock.Net" Version="1.25.0" />

        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMQ()
            .UseWireMock()                       // starts the stub server
            .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
            .StartAsync();
        // end-snippet
    }

    // begin-snippet: gs-wiremock-test
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
    // end-snippet

    // begin-snippet: gs-dump-logs
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
    // end-snippet

    public async Task GetContainerLogs()
    {
        // begin-snippet: gs-get-container-logs
        var (stdout, stderr) = await _env.GetEndpointContainerLogsAsync("YourEndpoint");
        // end-snippet
    }

    public async Task AgentTimeout()
    {
        string srcDir = null!;

        // begin-snippet: gs-agent-timeout
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMQ()
            .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
            .WithAgentConnectionTimeout(TimeSpan.FromMinutes(5))
            .StartAsync();
        // end-snippet
    }

    public async Task SkipBootstrap()
    {
        // begin-snippet: gs-skip-bootstrap
        // YourEndpoint.Testing/Program.cs
        await IntegrationTestingBootstrap.RunAsync(
            "YourEndpoint",
            YourEndpointConfig.Create,
            scenarios: [new SomeCommandScenario()],
            skipRules: [SkipRule.For<ProcessPayment>()]);
        // end-snippet
    }

    public async Task SkipObservation()
    {
        string correlationId = null!;

        // begin-snippet: gs-skip-observation
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var results = await _env.Observe(correlationId, cts.Token)
            .MessageSkipped("ProcessPayment")
            .WhenAllAsync();

        var skip = results.MessageSkipped("ProcessPayment");
        Assert.That(skip.EndpointName, Is.EqualTo("YourEndpoint"));
        // end-snippet
    }

    public async Task SkipPredicate()
    {
        // begin-snippet: gs-skip-predicate
        // YourEndpoint.Testing/Program.cs
        await IntegrationTestingBootstrap.RunAsync(
            "YourEndpoint",
            YourEndpointConfig.Create,
            scenarios: [new SomeCommandScenario()],
            skipRules: [SkipRule.For<ProcessPayment>(msg => msg.Amount > 1000)]);
        // end-snippet
    }

    // begin-snippet: gs-restart-endpoint
    [SetUp]
    public Task RestartBeforeEachTest() =>
        _env.RestartEndpointAsync("YourEndpoint");
    // end-snippet

    [Test]
    public async Task RestartInfrastructure()
    {
        // begin-snippet: gs-restart-infrastructure
        // Simulate a RabbitMQ restart mid-test
        await _env.RestartInfrastructureAsync(RabbitMqContainerOptions.InfrastructureKey);
        // ... observe endpoint reconnects and resumes processing
        // end-snippet
    }

    // begin-snippet: gs-stop-start-endpoint
    [Test]
    public async Task System_handles_endpoint_being_temporarily_down()
    {
        // Stop the endpoint to simulate a crash or planned shutdown.
        await _env.StopEndpointAsync("YourEndpoint");

        // ... trigger work on other endpoints, assert retry/compensation behaviour ...

        // Bring the endpoint back up; blocks until the agent reconnects.
        await _env.StartEndpointAsync("YourEndpoint");
    }
    // end-snippet
}

class WireMockConsumerHandler
{
    readonly HttpClient _http = new();

    async Task UseExternalService(CancellationToken ct)
    {
        // begin-snippet: gs-wiremock-endpoint
        // Only calls the external service when the variable is set (i.e., in test mode)
        var externalUrl = Environment.GetEnvironmentVariable("WIREMOCK_URL");
        if (externalUrl is not null)
        {
            var response = await _http.GetStringAsync($"{externalUrl}/api/data", ct);
        }
        // end-snippet
    }
}

static class ApiReferenceExamples
{
    public static async Task Bootstrap()
    {
        // begin-snippet: gs-api-bootstrap
        await IntegrationTestingBootstrap.RunAsync(
            endpointName:       "YourEndpoint",
            configFactory:      YourEndpointConfig.Create,
            scenarios:          [new SomeCommandScenario()],                                        // optional
            timeoutRules:       [TimeoutRule.For<OrderProcessingTimeout>(TimeSpan.FromSeconds(5))], // optional
            skipRules:          [SkipRule.For<ProcessPayment>()],                                    // optional
            sigTermGracePeriod: TimeSpan.FromSeconds(10));                                          // optional, default 5 s
        // end-snippet
    }

    public static void TimeoutRuleExamples()
    {
        // begin-snippet: gs-api-timeout-rule
        // Replace the scheduled delay for T with a fixed value
        TimeoutRule.For<OrderProcessingTimeout>(TimeSpan.FromSeconds(5));

        // Compute the delay from the timeout message instance
        TimeoutRule.For<OrderProcessingTimeout>(msg => msg.CustomDelay);
        // end-snippet
    }

    public static void SkipRuleExamples()
    {
        // begin-snippet: gs-api-skip-rule
        // ACK all messages of type T without invoking any handlers
        SkipRule.For<ProcessPayment>();

        // ACK only messages of type T that satisfy the predicate
        SkipRule.For<ProcessPayment>(msg => msg.Amount > 1000);
        // end-snippet
    }
}
