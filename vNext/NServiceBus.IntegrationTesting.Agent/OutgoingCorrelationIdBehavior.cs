using NServiceBus.Pipeline;

namespace NServiceBus.IntegrationTesting.Agent;

/// <summary>
/// Outgoing pipeline behavior that stamps the active test correlation ID onto
/// every message dispatched during a scenario or handler execution.
///
/// The correlation ID travels via AsyncLocal (set by AgentService.ExecuteScenarioAsync
/// and by ReportingBehavior before calling next()). This ensures it flows into:
///   - Messages sent directly by a Scenario via IMessageSession.Send()
///   - Messages sent/published/replied from within any handler in the chain
///
/// The receiving endpoint reads the header in ReportingBehavior and includes
/// it in the HandlerInvokedMessage streamed back to the test host.
/// </summary>
sealed class OutgoingCorrelationIdBehavior : Behavior<IOutgoingLogicalMessageContext>
{
    public override async Task Invoke(IOutgoingLogicalMessageContext context, Func<Task> next)
    {
        var correlationId = AgentService.CurrentCorrelationId.Value;
        if (correlationId is not null)
            context.Headers[AgentService.CorrelationIdHeader] = correlationId;

        await next();
    }
}
