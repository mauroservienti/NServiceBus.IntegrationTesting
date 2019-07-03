using MyMessages.Messages;
using NServiceBus;
using System.Threading.Tasks;

namespace MyOtherService
{
    public class AMessageHandler : IHandleMessages<AMessage>
    {
        public Task Handle(AMessage message, IMessageHandlerContext context)
        {
            return context.Reply(new AReplyMessage() { ThisWillBeTheSagaId = message.ThisWillBeTheSagaId });
        }
    }
}
