namespace SampleEndpoint.Handlers;

/// <summary>
/// Sent by SomeReplySaga when the timeout fires.
/// SagaCompletedMessageHandler handles this — its invocation is the test done condition.
/// </summary>
class SagaCompletedMessage : NServiceBus.IMessage
{
    public Guid SagaCorrelationId { get; set; }
}
