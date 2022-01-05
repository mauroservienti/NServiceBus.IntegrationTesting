using Grpc.Core;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    public class RemoteEndpointServer
    {
        public RemoteEndpointServer()
        {
            Server server = new Server
            {
                Services = { RemoteEndpoint.BindService(new RemoteEndpointImpl()) },
                //TODO: replace with configurable port
                Ports = { new ServerPort("localhost", 30051, ServerCredentials.Insecure) }
            };
            server.Start();
        }
    }
}
