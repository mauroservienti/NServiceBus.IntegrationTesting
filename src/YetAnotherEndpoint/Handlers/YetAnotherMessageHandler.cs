using SampleMessages;

namespace YetAnotherEndpoint.Handlers;

class YetAnotherMessageHandler : IHandleMessages<YetAnotherMessage>
{
    public Task Handle(YetAnotherMessage message, IMessageHandlerContext context)
    {
        Console.WriteLine($"[YetAnotherEndpoint] Handling YetAnotherMessage {message.CorrelationId}");
        return Task.CompletedTask;
    }
}
