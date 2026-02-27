using NServiceBus.IntegrationTesting.Containers;
using NUnit.Framework;

namespace NServiceBus.IntegrationTesting.Containers.Tests;

[TestFixture]
public class ObserveResultsTests
{
    static HandlerInvokedEvent MakeHandlerEvent(string correlationId = "c1", string handlerTypeName = "Handler") =>
        new(EndpointName: "EP", HandlerTypeName: handlerTypeName, MessageTypeName: "Msg",
            CorrelationId: correlationId, IsSaga: false, SagaNotFound: false,
            SagaTypeName: "", SagaId: "", SagaIsNew: false, SagaIsCompleted: false);

    static MessageDispatchedEvent MakeDispatchEvent(string correlationId = "c1", string messageTypeName = "Msg") =>
        new(EndpointName: "EP", MessageTypeName: messageTypeName, Intent: "Send", CorrelationId: correlationId);

    // ── HandlerInvocations ────────────────────────────────────────────────────

    [Test]
    public void HandlerInvocations_returns_list_when_key_exists()
    {
        var evt = MakeHandlerEvent(handlerTypeName: "MyHandler");
        var results = new ObserveResults(
            new Dictionary<string, IReadOnlyList<HandlerInvokedEvent>> { ["MyHandler"] = [evt] },
            [],
            []);

        var list = results.HandlerInvocations("MyHandler");

        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0], Is.SameAs(evt));
    }

    [Test]
    public void HandlerInvocations_throws_when_key_missing()
    {
        var results = new ObserveResults([], [], []);

        var ex = Assert.Throws<InvalidOperationException>(
            () => results.HandlerInvocations("Missing"));

        Assert.That(ex!.Message, Does.Contain("Missing"));
    }

    [Test]
    public void HandlerInvoked_returns_last_event()
    {
        var first = MakeHandlerEvent(handlerTypeName: "H");
        var last = MakeHandlerEvent(handlerTypeName: "H");
        var results = new ObserveResults(
            new Dictionary<string, IReadOnlyList<HandlerInvokedEvent>> { ["H"] = [first, last] },
            [],
            []);

        Assert.That(results.HandlerInvoked("H"), Is.SameAs(last));
    }

    // ── SagaInvocations ───────────────────────────────────────────────────────

    [Test]
    public void SagaInvocations_returns_list_from_saga_bucket()
    {
        var evt = MakeHandlerEvent(handlerTypeName: "MySaga");
        var results = new ObserveResults(
            [],
            new Dictionary<string, IReadOnlyList<HandlerInvokedEvent>> { ["MySaga"] = [evt] },
            []);

        var list = results.SagaInvocations("MySaga");

        Assert.That(list, Has.Count.EqualTo(1));
    }

    [Test]
    public void SagaInvocations_throws_when_key_missing()
    {
        var results = new ObserveResults([], [], []);

        var ex = Assert.Throws<InvalidOperationException>(
            () => results.SagaInvocations("MissingSaga"));

        Assert.That(ex!.Message, Does.Contain("MissingSaga"));
    }

    [Test]
    public void SagaInvocations_is_independent_from_handler_bucket()
    {
        // A saga name in the handler bucket does NOT satisfy SagaInvocations lookup.
        var evt = MakeHandlerEvent(handlerTypeName: "MySaga");
        var results = new ObserveResults(
            new Dictionary<string, IReadOnlyList<HandlerInvokedEvent>> { ["MySaga"] = [evt] },
            [],
            []);

        Assert.Throws<InvalidOperationException>(() => results.SagaInvocations("MySaga"));
    }

    [Test]
    public void SagaInvoked_returns_last_event()
    {
        var first = MakeHandlerEvent(handlerTypeName: "S");
        var last = MakeHandlerEvent(handlerTypeName: "S");
        var results = new ObserveResults(
            [],
            new Dictionary<string, IReadOnlyList<HandlerInvokedEvent>> { ["S"] = [first, last] },
            []);

        Assert.That(results.SagaInvoked("S"), Is.SameAs(last));
    }

    // ── MessageDispatches ─────────────────────────────────────────────────────

    [Test]
    public void MessageDispatches_returns_list_when_key_exists()
    {
        var evt = MakeDispatchEvent(messageTypeName: "OrderPlaced");
        var results = new ObserveResults(
            [],
            [],
            new Dictionary<string, IReadOnlyList<MessageDispatchedEvent>> { ["OrderPlaced"] = [evt] });

        var list = results.MessageDispatches("OrderPlaced");

        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0], Is.SameAs(evt));
    }

    [Test]
    public void MessageDispatches_throws_when_key_missing()
    {
        var results = new ObserveResults([], [], []);

        var ex = Assert.Throws<InvalidOperationException>(
            () => results.MessageDispatches("Ghost"));

        Assert.That(ex!.Message, Does.Contain("Ghost"));
    }

    [Test]
    public void MessageDispatched_returns_last_event()
    {
        var first = MakeDispatchEvent(messageTypeName: "M");
        var last = MakeDispatchEvent(messageTypeName: "M");
        var results = new ObserveResults(
            [],
            [],
            new Dictionary<string, IReadOnlyList<MessageDispatchedEvent>> { ["M"] = [first, last] });

        Assert.That(results.MessageDispatched("M"), Is.SameAs(last));
    }
}
