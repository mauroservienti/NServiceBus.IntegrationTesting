using NServiceBus.Pipeline;
using NServiceBus.Sagas;

namespace NServiceBus.IntegrationTesting.Agent;

/// <summary>
/// Pipeline behavior that intercepts handler invocations and reports them
/// to the test host via the AgentService gRPC channel.
/// Registered as a singleton instance — no DI resolution needed.
/// </summary>
sealed class ReportingBehavior : Behavior<IInvokeHandlerContext>
{
    readonly AgentService _agentService;

    public ReportingBehavior(AgentService agentService)
    {
        _agentService = agentService;
    }

    public override async Task Invoke(IInvokeHandlerContext context, Func<Task> next)
    {
        // Correlation ID was already extracted from the transport headers and stored
        // in CurrentCorrelationId by IncomingCorrelationIdBehavior earlier in the pipeline.
        var correlationId = AgentService.CurrentCorrelationId.Value;

        await next();

        // Only report on success. Failed invocations are transient noise — NServiceBus
        // will retry them. The test host only cares about eventual successful outcomes;
        // permanent failures are surfaced via MessageFailedMessage (error queue hook).
        // Use CancellationToken.None: the context token may already be spent at this point.
        var sagaInfo = BuildSagaInfo(context);
        if (sagaInfo is null)
            await _agentService.ReportHandlerInvokedAsync(
                context.MessageHandler.HandlerType.Name,
                context.MessageMetadata.MessageType.Name,
                correlationId,
                CancellationToken.None);
        else
            await _agentService.ReportSagaInvokedAsync(
                context.MessageHandler.HandlerType.Name,
                context.MessageMetadata.MessageType.Name,
                correlationId,
                sagaInfo,
                CancellationToken.None);
    }

    static SagaInfo? BuildSagaInfo(IInvokeHandlerContext context)
    {
        if (!context.Extensions.TryGet(out ActiveSagaInstance? saga) || saga is null)
            return null;

        if (saga.NotFound)
            return new SagaInfo(NotFound: true, TypeName: string.Empty, Id: string.Empty,
                IsNew: false, IsCompleted: false);

        return new SagaInfo(
            NotFound: false,
            TypeName: saga.Instance.GetType().Name,
            Id: saga.Instance.Entity.Id.ToString(),
            IsNew: saga.IsNew,
            IsCompleted: saga.Instance.Completed);
    }
}
