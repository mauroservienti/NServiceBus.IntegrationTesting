namespace SampleMessages;

public class AnotherMessage : NServiceBus.IMessage
{
    public Guid CorrelationId { get; set; }
}
