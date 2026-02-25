using NServiceBus.Pipeline;

namespace NServiceBus.IntegrationTesting.Agent;

/// <summary>
/// Incoming pipeline behavior registered at the transport receive stage — the
/// earliest point in the NSB pipeline. Extracts the test correlation ID from
/// the incoming message headers and stores it in AgentService.CurrentCorrelationId
/// so that every subsequent behavior (including ReportingBehavior and any
/// outgoing sends triggered by handlers) can read it without touching headers.
/// </summary>
sealed class IncomingCorrelationIdBehavior : Behavior<ITransportReceiveContext>
{
    public override async Task Invoke(ITransportReceiveContext context, Func<Task> next)
    {
        context.Message.Headers.TryGetValue(AgentService.CorrelationIdHeader, out var correlationId);
        AgentService.CurrentCorrelationId.Value = correlationId;

        await next();
    }
}
