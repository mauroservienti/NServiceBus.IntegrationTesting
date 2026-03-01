namespace SampleMessages;

public class YetAnotherMessage : NServiceBus.IMessage
{
    public Guid CorrelationId { get; set; }
}
