using NServiceBus;
using SampleMessages;

namespace AnotherEndpoint.Handlers;

class FailingMessageHandler : IHandleMessages<FailingMessage>
{
    public Task Handle(FailingMessage message, IMessageHandlerContext context)
    {
        throw new InvalidOperationException(
            $"Intentional failure for FailingMessage {message.CorrelationId}");
    }
}
