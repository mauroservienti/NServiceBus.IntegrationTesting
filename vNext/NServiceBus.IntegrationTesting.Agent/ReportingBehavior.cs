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

        Exception? handlingError = null;
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            handlingError = ex;
            throw;
        }
        finally
        {
            // Report regardless of success or failure so the test host
            // can observe both happy-path and error scenarios.
            // Use CancellationToken.None: we want to report even when the message
            // processing was cancelled or failed — the context token may already
            // be cancelled at this point in the finally block.
            await _agentService.ReportHandlerInvokedAsync(
                context.MessageHandler.HandlerType.Name,
                context.MessageMetadata.MessageType.Name,
                correlationId,
                handlingError is not null,
                handlingError?.Message,
                BuildSagaInfo(context),
                CancellationToken.None);
        }
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
