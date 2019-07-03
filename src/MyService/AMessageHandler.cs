using MyMessages.Messages;
using NServiceBus;
using System.Threading.Tasks;

namespace MyService
{
    public class AMessageHandler : IHandleMessages<AMessage>
    {
        public Task Handle(AMessage message, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }
    }
}
