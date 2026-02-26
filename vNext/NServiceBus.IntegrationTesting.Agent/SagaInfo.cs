namespace NServiceBus.IntegrationTesting.Agent;

/// <summary>
/// Snapshot of saga state captured at handler invocation time.
/// Passed from ReportingBehavior to AgentService and serialised into HandlerInvokedMessage.
/// </summary>
internal record SagaInfo(
    bool NotFound,
    string TypeName,
    string Id,
    bool IsNew,
    bool IsCompleted);
