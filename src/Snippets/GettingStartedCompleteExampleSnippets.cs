using NServiceBus;
using NServiceBus.IntegrationTesting;
using NServiceBus.IntegrationTesting.Agent;
using NServiceBus.Persistence.Sql;
using NServiceBus.Transport.RabbitMQ;
using Npgsql;
using NpgsqlTypes;
using NUnit.Framework;
using SampleEndpoint.Handlers;
using SampleEndpoint.Testing;
using SampleMessages;

namespace Snippets.CompleteExample;

// begin-snippet: gs-complete-config
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
// end-snippet

public static class CompleteBootstrap
{
    public static async Task Run()
    {
        // begin-snippet: gs-complete-bootstrap
        // SampleEndpoint.Testing/Program.cs
        await IntegrationTestingBootstrap.RunAsync(
            "SampleEndpoint",
            SampleEndpointConfig.Create,
            scenarios: [new SomeMessageScenario(), new FailingMessageScenario()],
            timeoutRules: [TimeoutRule.For<SomeReplySagaTimeout>(TimeSpan.FromSeconds(5))]);
        // end-snippet
    }
}

// begin-snippet: gs-complete-scenario
// SampleEndpoint.Testing/SomeMessageScenario.cs
public class SomeMessageScenario : Scenario
{
    public override string Name => "SomeMessage Scenario";

    public override async Task Execute(
        IMessageSession session,
        Dictionary<string, string> args,
        CancellationToken cancellationToken = default)
        => await session.Send(new SomeMessage { Id = Guid.Parse(args["ID"]) });
}
// end-snippet

// begin-snippet: gs-complete-test
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
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(TestEnvironmentBuilder.FindRootByDirectory(".git", "src"))
            .UseRabbitMQ()
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
            "SomeMessage Scenario",
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

}
// end-snippet
