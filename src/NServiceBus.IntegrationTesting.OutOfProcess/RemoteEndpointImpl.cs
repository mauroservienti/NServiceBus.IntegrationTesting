using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System;
using System.Threading.Tasks;
using static NServiceBus.IntegrationTesting.OutOfProcess.RemoteEndpoint;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    class RemoteEndpointImpl : RemoteEndpointBase
    {
        bool endpoitStarted;
        readonly string endpointName;

        public RemoteEndpointImpl(string endpointName)
        {
            this.endpointName = endpointName;
        }

        public override Task<Empty> Stop(Empty request, ServerCallContext context)
        {
            Environment.Exit(0);

            return Task.FromResult(new Empty());
        }

        public override async Task EndpointStarted(Empty request, IServerStreamWriter<EndpointStartedEvent> responseStream, ServerCallContext context)
        {
            while (!endpoitStarted)
            {
                await Task.Delay(500);
            }

            await responseStream.WriteAsync(new EndpointStartedEvent() 
            { 
                EndpointName = endpointName 
            });
        }

        internal void NotifyEndpointStarted()
        {
            endpoitStarted = true;
        }
    }
}
