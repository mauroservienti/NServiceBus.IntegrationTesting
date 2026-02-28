using NServiceBus.IntegrationTesting;
using NUnit.Framework;

namespace Snippets.GettingStartedObserving;

[TestFixture]
public class ObservingSnippets
{
    static TestEnvironment _env = null!;
    static EndpointHandle _yourEndpoint = null!;

    public async Task ExecuteScenario()
    {
        // begin-snippet: gs-execute-scenario
        var correlationId = await _yourEndpoint.ExecuteScenarioAsync(
            "SomeCommand Scenario",
            new Dictionary<string, string> { { "ID", Guid.NewGuid().ToString() } });
        // end-snippet
    }

    public async Task HandlerInvocation()
    {
        string correlationId = null!;
        using var cts = new CancellationTokenSource();

        // begin-snippet: gs-handler-invocation
        var results = await _env.Observe(correlationId, cts.Token)
            .HandlerInvoked("SomeMessageHandler")
            .HandlerInvoked("AnotherMessageHandler")
            .WhenAllAsync();

        // The last (or only) invocation of the handler:
        var evt = results.HandlerInvoked("SomeMessageHandler");
        Assert.That(evt.EndpointName, Is.EqualTo("YourEndpoint"));
        // end-snippet
    }

    public async Task SagaInvocation()
    {
        string correlationId = null!;
        using var cts = new CancellationTokenSource();

        // begin-snippet: gs-saga-invocation
        var results = await _env.Observe(correlationId, cts.Token)
            .SagaInvoked("OrderSaga")
            .WhenAllAsync();

        var sagaEvt = results.SagaInvoked("OrderSaga");
        Assert.Multiple(() =>
        {
            Assert.That(sagaEvt.IsSaga, Is.True);
            Assert.That(sagaEvt.SagaIsNew, Is.True);
            Assert.That(sagaEvt.SagaIsCompleted, Is.False);
        });
        // end-snippet
    }

    public async Task MessageDispatch()
    {
        string correlationId = null!;
        using var cts = new CancellationTokenSource();

        // begin-snippet: gs-message-dispatch
        var results = await _env.Observe(correlationId, cts.Token)
            .MessageDispatched("AnotherMessage")
            .WhenAllAsync();

        var dispatch = results.MessageDispatched("AnotherMessage");
        Assert.Multiple(() =>
        {
            Assert.That(dispatch.EndpointName, Is.EqualTo("YourEndpoint"));
            Assert.That(dispatch.Intent, Is.EqualTo("Send"));   // or "Publish", "Reply", "RequestTimeout"
        });
        // end-snippet
    }

    public async Task MessageFailure()
    {
        string correlationId = null!;
        using var cts = new CancellationTokenSource();

        // begin-snippet: gs-message-failure
        var results = await _env.Observe(correlationId, cts.Token)
            .MessageFailed()
            .WhenAllAsync();

        var failure = results.MessageFailed();
        Assert.Multiple(() =>
        {
            Assert.That(failure.EndpointName, Is.EqualTo("AnotherEndpoint"));
            Assert.That(failure.ExceptionMessage, Does.Contain("expected error text"));
            // The message type is available via NServiceBus headers:
            Assert.That(failure.Headers["NServiceBus.EnclosedMessageTypes"],
                Does.Contain("FailingMessage"));
        });
        // end-snippet
    }

    public async Task FastFail()
    {
        string correlationId = null!;
        using var cts = new CancellationTokenSource();

        // begin-snippet: gs-fast-fail
        // MessageFailedException is thrown if the handler fails permanently,
        // even though the test only registered success conditions.
        var results = await _env.Observe(correlationId, cts.Token)
            .HandlerInvoked("SomeMessageHandler")
            .WhenAllAsync();
        // end-snippet
    }
}
