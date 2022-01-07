using Grpc.Core;
using System.Text.Json;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    public class RemoteEndpointClient : IRemoteMessageSessionProxy
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
            //that kills the stream that blows up the client if this awaits
            _ = client.StopAsync(new Google.Protobuf.WellKnownTypes.Empty());
            await channel.ShutdownAsync();
        }

        public async Task Send<TMessage>(string destination, TMessage message)
        {
            var sendRequest = new SendRequest()
            {
                Destination = destination,
                JsonMessage = JsonSerializer.Serialize(message),
                JsonMessageType = message.GetType().AssemblyQualifiedName
            };

            await client.SendAsync(sendRequest);
        }

        public Task Send<TMessage>(TMessage message)
        {
            return Send(null, message);
        }
    }
}
