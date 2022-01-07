using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.OutOfProcess.Nsb8
{
    internal class RemoteEndpointServerV8 : RemoteEndpointServer
    {
        private IMessageSession messageSession;

        public RemoteEndpointServerV8()
        {
            OnSendRequest= r =>
            {
                var messageType = Type.GetType(r.JsonMessageType);
                var message = JsonSerializer.Deserialize(r.JsonMessage, messageType);

                if (string.IsNullOrWhiteSpace(r.Destination))
                {
                    return messageSession.Send(message);
                }
                else
                {
                    return messageSession.Send(r.Destination, message);
                }
            };
        }

        protected override Func<SendRequest, Task> OnSendRequest { get; }

        public void RegisterMessageSession(IMessageSession messageSession)
        {
            this.messageSession = messageSession;
        }
    }
}
