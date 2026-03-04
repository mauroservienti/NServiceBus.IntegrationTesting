using NServiceBus.IntegrationTesting;
using NUnit.Framework;

namespace SampleEndpoint.Tests;

/// <summary>
/// Proves that a message permanently sent to the error queue is surfaced via
/// the MessageFailed condition — and that the reported exception message matches
/// the one thrown by the handler.
///
/// Flow:
///   1. SampleEndpoint executes the "FailingMessage" scenario, sending FailingMessage to AnotherEndpoint.
///   2. AnotherEndpoint's FailingMessageHandler throws immediately.
///   3. With 0 retries configured in AnotherEndpoint.Testing, the message goes straight to the error queue.
///   4. AnotherEndpoint's agent reports a MessageFailedEvent to the test host.
///   5. The test asserts endpoint name, message type name, and exception message.
/// </summary>
[TestFixture]
[NonParallelizable]
public class WhenAMessageFails
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
    public async Task The_failure_should_be_reported_to_the_test_host()
    {
        var args = new Dictionary<string, string>
        {
            { "ID", Guid.NewGuid().ToString() }
        };
        var correlationId = await _sampleEndpoint.ExecuteScenarioAsync("FailingMessage", args);
        TestContext.Out.WriteLine($"Correlation ID: {correlationId}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var results = await _env.Observe(correlationId, cts.Token)
            .MessageFailed()
            .WhenAllAsync();

        var failure = results.MessageFailed();
        Assert.Multiple(() =>
        {
            Assert.That(failure.EndpointName, Is.EqualTo("AnotherEndpoint"));
            Assert.That(failure.Headers["NServiceBus.EnclosedMessageTypes"].Contains("FailingMessage"), Is.True);
            Assert.That(failure.ExceptionMessage, Does.Contain("Intentional failure"));
        });
    }
}
