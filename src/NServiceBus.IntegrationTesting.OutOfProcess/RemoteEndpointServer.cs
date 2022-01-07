using Grpc.Core;
using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    public abstract class RemoteEndpointServer
    {
        Server server;

        public void Start()
        {
            server = new Server
            {
                Services = { RemoteEndpoint.BindService(new RemoteEndpointImpl(OnSendRequest)) },
                //TODO: replace with configurable port
                Ports = { new ServerPort("localhost", 30051, ServerCredentials.Insecure) }
            };
            server.Start();
        }

        protected abstract Func<SendRequest, Task> OnSendRequest { get; }
    }
}
