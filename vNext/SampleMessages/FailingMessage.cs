namespace SampleMessages;

public class FailingMessage : NServiceBus.IMessage
{
    public Guid CorrelationId { get; set; }
}
