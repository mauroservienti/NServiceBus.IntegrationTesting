using NServiceBus;

namespace SampleEndpoint.Handlers;

class SagaCompletedMessageHandler : IHandleMessages<SagaCompletedMessage>
{
    public Task Handle(SagaCompletedMessage message, IMessageHandlerContext context)
    {
        Console.WriteLine($"[SampleEndpoint] Saga completed for correlation {message.SagaCorrelationId}");
        return Task.CompletedTask;
    }
}
