using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System;
using System.Threading.Tasks;
using static NServiceBus.IntegrationTesting.OutOfProcess.RemoteEndpoint;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    class RemoteEndpointImpl : RemoteEndpointBase
    {
        public override Task<Empty> Stop(Empty request, ServerCallContext context)
        {
            Environment.Exit(0);

            return Task.FromResult(new Empty());
        }
    }
}
