using Grpc.Core;
using NServiceBus.IntegrationTesting.OutOfProcess.Grpc;
using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    public abstract class RemoteEndpointServer
    {
        Server server;
        private readonly int port;

        protected RemoteEndpointServer(int port)
        {
            this.port = port;
        }

        public void Start()
        {
            server = new Server
            {
                Services = { RemoteEndpoint.BindService(new RemoteEndpointImpl(OnSendRequest)) },
                //TODO: replace with configurable port
                Ports = { new ServerPort("localhost", port, ServerCredentials.Insecure) }
            };
            server.Start();
        }

        protected abstract Func<SendRequest, Task> OnSendRequest { get; }
    }
}
