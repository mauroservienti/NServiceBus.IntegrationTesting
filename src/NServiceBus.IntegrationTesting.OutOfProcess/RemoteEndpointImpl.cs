using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System;
using System.Threading.Tasks;
using static NServiceBus.IntegrationTesting.OutOfProcess.RemoteEndpoint;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    class RemoteEndpointImpl : RemoteEndpointBase
    {
        readonly Func<SendRequest, Task> onSendRequest;

        public RemoteEndpointImpl(Func<SendRequest, Task> onSendRequest)
        {
            this.onSendRequest = onSendRequest;
        }

        public override Task<Empty> Stop(Empty request, ServerCallContext context)
        {
            Environment.Exit(0);

            return Task.FromResult(new Empty());
        }

        public override async Task<Empty> Send(SendRequest request, ServerCallContext context)
        {
            await onSendRequest(request);

            return new Empty();
        }
    }
}
