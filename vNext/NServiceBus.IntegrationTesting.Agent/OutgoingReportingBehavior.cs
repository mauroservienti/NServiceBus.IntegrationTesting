using NServiceBus.Pipeline;

namespace NServiceBus.IntegrationTesting.Agent;

/// <summary>
/// Outgoing pipeline behavior that reports every dispatched message back to the test host.
/// Only fires when a test correlation ID is active (i.e., inside a scenario or handler chain).
/// Reports in finally so dispatch errors are also captured.
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

        Exception? dispatchError = null;
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            dispatchError = ex;
            throw;
        }
        finally
        {
            try
            {
                await agentService.ReportMessageDispatchedAsync(
                    context.Message.MessageType.Name,
                    intent,
                    correlationId,
                    dispatchError is not null,
                    dispatchError?.Message,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Agent] ReportMessageDispatchedAsync failed: {ex.Message}");
            }
        }
    }
}
