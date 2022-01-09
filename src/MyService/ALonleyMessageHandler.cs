using MyMessages.Messages;
using NServiceBus;
using System.Threading.Tasks;

namespace MyService
{
    class ALonleyMessageHandler : IHandleMessages<ALonleyMessage>
    {
        public Task Handle(ALonleyMessage message, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }
    }
}
