using MyMessages.Messages;
using NServiceBus;
using System.Threading.Tasks;

namespace MyService
{
    public class AReplyMessageHandler : IHandleMessages<AReplyMessage>
    {
        public Task Handle(AReplyMessage message, IMessageHandlerContext context)
        {
            return context.SendLocal(new StartASaga() { AnIdentifier = message.AnIdentifier });
        }
    }
}
