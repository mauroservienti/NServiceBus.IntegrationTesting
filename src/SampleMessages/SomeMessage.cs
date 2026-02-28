namespace SampleMessages;

public class SomeMessage : NServiceBus.IMessage
{
    public Guid Id { get; set; }
}
