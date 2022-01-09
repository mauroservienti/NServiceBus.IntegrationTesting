using NServiceBus.IntegrationTesting.OutOfProcess.Grpc;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.OutOfProcess.Nsb8
{
    internal class RemoteEndpointServerV8 : RemoteEndpointServer
    {
        private IMessageSession messageSession;

        public RemoteEndpointServerV8(int port)
            : base(port)
        {
            OnSendRequest= async r =>
            {
                var messageType = Type.GetType(r.JsonMessageType);
                var message = JsonSerializer.Deserialize(r.JsonMessage, messageType);

                try
                {
                    if (string.IsNullOrWhiteSpace(r.Destination))
                    {
                        await messageSession.Send(message).ConfigureAwait(false);
                    }
                    else
                    {
                        await messageSession.Send(r.Destination, message).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) 
                {
                    throw;
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
