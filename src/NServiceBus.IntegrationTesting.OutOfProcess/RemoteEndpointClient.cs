using Grpc.Core;
using System;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    public class RemoteEndpointClient
    {
        private Channel channel;
        private RemoteEndpoint.RemoteEndpointClient client;

        public RemoteEndpointClient()
        {
            //TODO: replace with configurable port
            channel = new Channel("127.0.0.1:30051", ChannelCredentials.Insecure);
            client = new RemoteEndpoint.RemoteEndpointClient(channel);
        }

        public async Task Stop()
        {
            //cannot await: stopping the remote process kills the server
            //that kills the stream tat blows up the client if awaits
            _ = client.StopAsync(new Google.Protobuf.WellKnownTypes.Empty());
            await channel.ShutdownAsync();
        }

        public async Task OnEndpointStarted(Func<EndpointStartedEvent, Task> onStarted)
        {
            var response = client.EndpointStarted(new Google.Protobuf.WellKnownTypes.Empty());
            while (await response.ResponseStream.MoveNext())
            {
                var current = response.ResponseStream.Current;
                await onStarted(current);
            }
        }
    }
}
