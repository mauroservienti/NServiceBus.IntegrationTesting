namespace SampleMessages;

public class SomeReply : NServiceBus.IMessage
{
    public Guid CorrelationId { get; set; }
}
