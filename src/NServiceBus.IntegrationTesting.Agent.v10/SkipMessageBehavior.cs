using NServiceBus.Pipeline;

namespace NServiceBus.IntegrationTesting.Agent;

/// <summary>
/// Incoming pipeline behavior that checks each message against registered SkipRules.
/// When a rule matches, the message is ACKed without invoking any handlers and a
/// MessageSkippedMessage is reported to the test host.
/// Registered by IntegrationTestingBootstrap when skipRules are provided.
/// </summary>
class SkipMessageBehavior(IReadOnlyList<SkipRule> rules, AgentService agentService)
    : Behavior<IIncomingLogicalMessageContext>
{
    public override async Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
    {
        var messageType = context.Message.MessageType;
        var messageInstance = context.Message.Instance;
        // Correlation ID was already extracted from transport headers by
        // IncomingCorrelationIdBehavior earlier in the pipeline.
        var correlationId = AgentService.CurrentCorrelationId.Value;

        foreach (var rule in rules)
        {
            if (!rule.ShouldSkip(messageType, messageInstance))
                continue;

            await agentService.ReportMessageSkippedAsync(
                messageType.AssemblyQualifiedName ?? messageType.FullName ?? messageType.Name,
                correlationId,
                CancellationToken.None);

            return; // short-circuit — message is ACKed without calling any handlers
        }

        await next();
    }
}
