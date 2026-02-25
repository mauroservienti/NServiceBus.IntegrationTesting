using SampleMessages;

namespace SampleEndpoint.Handlers;

class SomeMessageHandler : IHandleMessages<SomeMessage>
{
    public async Task Handle(SomeMessage message, IMessageHandlerContext context)
    {
        Console.WriteLine($"[SampleEndpoint] Handling SomeMessage {message.Id}");
        await context.Send(new AnotherMessage { CorrelationId = message.Id });
    }
}
