using NServiceBus.Pipeline;

namespace NServiceBus.IntegrationTesting.Agent;

/// <summary>
/// Outgoing pipeline behavior that reports successfully dispatched messages to the test host.
/// Only fires when a test correlation ID is active (i.e., inside a scenario or handler chain).
/// Only reports after next() succeeds — failed dispatches are transient noise that NServiceBus
/// will retry; the test host only cares about messages that reached the transport.
/// </summary>
sealed class OutgoingReportingBehavior(AgentService agentService) : Behavior<IOutgoingLogicalMessageContext>
{
    public override async Task Invoke(IOutgoingLogicalMessageContext context, Func<Task> next)
    {
        var correlationId = AgentService.CurrentCorrelationId.Value;
        if (correlationId is null)
        {
            await next();
            return;
        }

        // Determine intent before next() while headers are still mutable.
        // Saga timeout requests are sends, but semantically distinct — detect them explicitly.
        string intent;
        if (context.Headers.TryGetValue(Headers.IsSagaTimeoutMessage, out var isTimeout)
            && isTimeout == bool.TrueString)
        {
            intent = "RequestTimeout";
        }
        else
        {
            context.Headers.TryGetValue(Headers.MessageIntent, out var intentHeader);
            intent = intentHeader ?? "Unknown";
        }

        await next();

        // Only report on success. Failed dispatches are transient noise — NServiceBus
        // will retry the enclosing message. The test host only cares about messages
        // that were successfully dispatched to the transport.
        try
        {
            await agentService.ReportMessageDispatchedAsync(
                context.Message.MessageType.Name,
                intent,
                correlationId,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Agent] ReportMessageDispatchedAsync failed: {ex.Message}");
        }
    }
}
