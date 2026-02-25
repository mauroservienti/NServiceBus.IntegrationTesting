using SampleMessages;

namespace SampleEndpoint.Handlers;

class SomeReplyHandler : IHandleMessages<SomeReply>
{
    public Task Handle(SomeReply message, IMessageHandlerContext context)
    {
        Console.WriteLine($"[SampleEndpoint] Handling SomeReply {message.CorrelationId}");
        return Task.CompletedTask;
    }
}
