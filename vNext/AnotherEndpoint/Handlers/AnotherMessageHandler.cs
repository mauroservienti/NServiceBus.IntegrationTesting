using SampleMessages;

namespace AnotherEndpoint.Handlers;

class AnotherMessageHandler : IHandleMessages<AnotherMessage>
{
    public async Task Handle(AnotherMessage message, IMessageHandlerContext context)
    {
        Console.WriteLine($"[AnotherEndpoint] Handling AnotherMessage {message.CorrelationId}");
        await context.Reply(new SomeReply { CorrelationId = message.CorrelationId });
    }
}
