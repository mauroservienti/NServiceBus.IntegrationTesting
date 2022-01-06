using Grpc.Core;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    public abstract class RemoteEndpointServer
    {
        private readonly RemoteEndpointImpl remoteEndpoint;
        readonly Server server;

        protected RemoteEndpointServer(string endpointName)
        {
            remoteEndpoint = new RemoteEndpointImpl(endpointName);
            EndpointName = endpointName;

            server = new Server
            {
                Services = { RemoteEndpoint.BindService(remoteEndpoint) },
                //TODO: replace with configurable port
                Ports = { new ServerPort("localhost", 30051, ServerCredentials.Insecure) }
            };
            server.Start();
        }

        public string EndpointName { get; }

        public void NotifyEndpointStarted()
        {
            remoteEndpoint.NotifyEndpointStarted();
        }
    }
}
