using System.Threading.Channels;
using NServiceBus.IntegrationTesting;
using NUnit.Framework;

namespace NServiceBus.IntegrationTesting.Tests;

/// <summary>
/// Unit tests for the three internal scan helpers in TestHostGrpcService.
/// Each helper reads from a ChannelReader and returns matching events once
/// the predicate/done condition is satisfied, or null when cancelled / channel closed.
/// </summary>
[TestFixture]
public class ScanHelpersTests
{
    const string Id = "corr-1";
    const string OtherId = "corr-other";

    // ── Helpers ───────────────────────────────────────────────────────────────

    static HandlerInvokedEvent HandlerEvent(string correlationId = Id, string handlerTypeName = "H") =>
        new(EndpointName: "EP", HandlerTypeName: handlerTypeName, MessageTypeName: "Msg",
            CorrelationId: correlationId, IsSaga: false, SagaNotFound: false,
            SagaTypeName: "", SagaId: "", SagaIsNew: false, SagaIsCompleted: false);

    static MessageDispatchedEvent DispatchEvent(string correlationId = Id, string messageTypeName = "M") =>
        new(EndpointName: "EP", MessageTypeName: messageTypeName, Intent: "Send", CorrelationId: correlationId);

    static MessageFailedEvent FailureEvent(string correlationId = Id) =>
        new(EndpointName: "EP", Headers: new Dictionary<string, string>(), ExceptionMessage: "boom", CorrelationId: correlationId);

    // ── ScanForHandlerEventsAsync ─────────────────────────────────────────────

    [Test]
    public async Task HandlerScan_returns_null_when_channel_closed_without_match()
    {
        var ch = Channel.CreateUnbounded<HandlerInvokedEvent>();
        ch.Writer.TryWrite(HandlerEvent(correlationId: OtherId));
        ch.Writer.Complete();

        var result = await TestHostGrpcService.ScanForHandlerEventsAsync(
            ch.Reader, Id, "H", static all => all.Count >= 1, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task HandlerScan_returns_null_when_cancelled_before_match()
    {
        var ch = Channel.CreateUnbounded<HandlerInvokedEvent>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await TestHostGrpcService.ScanForHandlerEventsAsync(
            ch.Reader, Id, "H", static all => all.Count >= 1, cts.Token);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task HandlerScan_ignores_events_with_wrong_correlationId()
    {
        var ch = Channel.CreateUnbounded<HandlerInvokedEvent>();
        ch.Writer.TryWrite(HandlerEvent(correlationId: OtherId, handlerTypeName: "H"));
        ch.Writer.Complete();

        var result = await TestHostGrpcService.ScanForHandlerEventsAsync(
            ch.Reader, Id, "H", static all => all.Count >= 1, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task HandlerScan_ignores_events_with_wrong_handlerTypeName()
    {
        var ch = Channel.CreateUnbounded<HandlerInvokedEvent>();
        ch.Writer.TryWrite(HandlerEvent(correlationId: Id, handlerTypeName: "Other"));
        ch.Writer.Complete();

        var result = await TestHostGrpcService.ScanForHandlerEventsAsync(
            ch.Reader, Id, "H", static all => all.Count >= 1, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task HandlerScan_returns_on_first_match_with_count_predicate()
    {
        var ch = Channel.CreateUnbounded<HandlerInvokedEvent>();
        var evt = HandlerEvent();
        ch.Writer.TryWrite(evt);
        // Do not complete the channel — the helper must return before the channel closes.

        var result = await TestHostGrpcService.ScanForHandlerEventsAsync(
            ch.Reader, Id, "H", static all => all.Count >= 1, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result![0], Is.SameAs(evt));
    }

    [Test]
    public async Task HandlerScan_list_predicate_does_not_return_until_n_matches()
    {
        var ch = Channel.CreateUnbounded<HandlerInvokedEvent>();
        var first = HandlerEvent();
        var second = HandlerEvent();
        ch.Writer.TryWrite(first);
        ch.Writer.TryWrite(second);

        var result = await TestHostGrpcService.ScanForHandlerEventsAsync(
            ch.Reader, Id, "H", static all => all.Count >= 2, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task HandlerScan_list_predicate_result_contains_accumulated_events()
    {
        // Write 3 events but predicate fires at 2 — result must have exactly 2.
        var ch = Channel.CreateUnbounded<HandlerInvokedEvent>();
        ch.Writer.TryWrite(HandlerEvent());
        ch.Writer.TryWrite(HandlerEvent());
        ch.Writer.TryWrite(HandlerEvent()); // would be third — should not be included

        var result = await TestHostGrpcService.ScanForHandlerEventsAsync(
            ch.Reader, Id, "H", static all => all.Count >= 2, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(2));
    }

    // ── ScanForDispatchEventsAsync ────────────────────────────────────────────

    [Test]
    public async Task DispatchScan_returns_null_when_channel_closed_without_match()
    {
        var ch = Channel.CreateUnbounded<MessageDispatchedEvent>();
        ch.Writer.TryWrite(DispatchEvent(correlationId: OtherId));
        ch.Writer.Complete();

        var result = await TestHostGrpcService.ScanForDispatchEventsAsync(
            ch.Reader, Id, "M", static all => all.Count >= 1, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task DispatchScan_returns_null_when_cancelled_before_match()
    {
        var ch = Channel.CreateUnbounded<MessageDispatchedEvent>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await TestHostGrpcService.ScanForDispatchEventsAsync(
            ch.Reader, Id, "M", static all => all.Count >= 1, cts.Token);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task DispatchScan_ignores_events_with_wrong_correlationId()
    {
        var ch = Channel.CreateUnbounded<MessageDispatchedEvent>();
        ch.Writer.TryWrite(DispatchEvent(correlationId: OtherId, messageTypeName: "M"));
        ch.Writer.Complete();

        var result = await TestHostGrpcService.ScanForDispatchEventsAsync(
            ch.Reader, Id, "M", static all => all.Count >= 1, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task DispatchScan_ignores_events_with_wrong_messageTypeName()
    {
        var ch = Channel.CreateUnbounded<MessageDispatchedEvent>();
        ch.Writer.TryWrite(DispatchEvent(correlationId: Id, messageTypeName: "Other"));
        ch.Writer.Complete();

        var result = await TestHostGrpcService.ScanForDispatchEventsAsync(
            ch.Reader, Id, "M", static all => all.Count >= 1, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task DispatchScan_returns_on_first_match()
    {
        var ch = Channel.CreateUnbounded<MessageDispatchedEvent>();
        var evt = DispatchEvent();
        ch.Writer.TryWrite(evt);

        var result = await TestHostGrpcService.ScanForDispatchEventsAsync(
            ch.Reader, Id, "M", static all => all.Count >= 1, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result![0], Is.SameAs(evt));
    }

    [Test]
    public async Task DispatchScan_list_predicate_returns_only_after_n_matches()
    {
        var ch = Channel.CreateUnbounded<MessageDispatchedEvent>();
        ch.Writer.TryWrite(DispatchEvent());
        ch.Writer.TryWrite(DispatchEvent());

        var result = await TestHostGrpcService.ScanForDispatchEventsAsync(
            ch.Reader, Id, "M", static all => all.Count >= 2, CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task DispatchScan_single_event_predicate_can_filter_on_intent()
    {
        var ch = Channel.CreateUnbounded<MessageDispatchedEvent>();
        // Wrong intent — should be skipped.
        ch.Writer.TryWrite(new MessageDispatchedEvent("EP", "M", "Send", Id));
        // Matching intent.
        var matching = new MessageDispatchedEvent("EP", "M", "RequestTimeout", Id);
        ch.Writer.TryWrite(matching);

        var result = await TestHostGrpcService.ScanForDispatchEventsAsync(
            ch.Reader, Id, "M",
            all => all.Count >= 1 && all[^1].Intent == "RequestTimeout",
            CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result![^1].Intent, Is.EqualTo("RequestTimeout"));
    }

    // ── ScanForFailureEventAsync ──────────────────────────────────────────────

    [Test]
    public async Task FailureScan_returns_matching_event()
    {
        var ch = Channel.CreateUnbounded<MessageFailedEvent>();
        var evt = FailureEvent();
        ch.Writer.TryWrite(evt);

        var result = await TestHostGrpcService.ScanForFailureEventAsync(
            ch.Reader, Id, CancellationToken.None);

        Assert.That(result, Is.SameAs(evt));
    }

    [Test]
    public async Task FailureScan_ignores_events_with_wrong_correlationId()
    {
        var ch = Channel.CreateUnbounded<MessageFailedEvent>();
        ch.Writer.TryWrite(FailureEvent(correlationId: OtherId));
        ch.Writer.Complete();

        var result = await TestHostGrpcService.ScanForFailureEventAsync(
            ch.Reader, Id, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task FailureScan_returns_null_when_cancelled()
    {
        var ch = Channel.CreateUnbounded<MessageFailedEvent>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await TestHostGrpcService.ScanForFailureEventAsync(
            ch.Reader, Id, cts.Token);

        Assert.That(result, Is.Null);
    }
}
