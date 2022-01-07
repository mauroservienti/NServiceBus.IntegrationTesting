using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using static NServiceBus.IntegrationTesting.OutOfProcess.OutOfProcessEndpointRunner;

namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    class OutOfProcessEndpointRunnerImpl : OutOfProcessEndpointRunnerBase
    {
        readonly Action<EndpointStartedEvent> onEndpointStarted;
        readonly Action<OutgoingMessageOperation> onOutgoingMessageOperation;
        private readonly Action<(string EndpointName, string PropertyName, string PropertyValue)> onSetContextProperty;

        public OutOfProcessEndpointRunnerImpl(
            Action<EndpointStartedEvent> onEndpointStarted, 
            Action<OutgoingMessageOperation> onOutgoingMessageOperation, 
            Action<(string EndpointName, string PropertyName, string PropertyValue)> onSetContextProperty
        )
        {
            this.onEndpointStarted = onEndpointStarted;
            this.onOutgoingMessageOperation = onOutgoingMessageOperation;
            this.onSetContextProperty = onSetContextProperty;
        }

        public override Task<Empty> OnEndpointStarted(EndpointStartedEvent request, ServerCallContext context)
        {
            onEndpointStarted(request);

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> RecordOutgoingMessageOperation(OutgoingMessageOperationEvent request, ServerCallContext context)
        {
            var operationType = System.Type.GetType(request.OperationTypeAssemblyQualifiedName);
            var operation = (OutgoingMessageOperation)Activator.CreateInstance(operationType);

            operation.SenderEndpoint = request.SenderEndpoint;
            operation.MessageId = request.MessageId;
            operation.MessageType = !string.IsNullOrWhiteSpace(request.MessageTypeAssemblyQualifiedName)
                ? System.Type.GetType(request.MessageTypeAssemblyQualifiedName) 
                : null;
            operation.MessageInstance = !string.IsNullOrWhiteSpace(request.MessageInstanceJson)
                ? JsonSerializer.Deserialize(request.MessageInstanceJson, operation.MessageType)
                : null;
            operation.MessageHeaders = !string.IsNullOrWhiteSpace(request.MessageHeadersJson)
                ? (Dictionary<string, string>)JsonSerializer.Deserialize(request.MessageHeadersJson, typeof(Dictionary<string, string>))
                : null;
            operation.OperationError = !string.IsNullOrWhiteSpace(request.OperationErrorJson)
                ? (Exception)JsonSerializer.Deserialize(request.OperationErrorJson, System.Type.GetType(request.OperationErrorTypeAssemblyQualifiedName))
                : null;

            onOutgoingMessageOperation(operation);

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> SetContextProperty(SetContextPropertyRequest request, ServerCallContext context)
        {
            onSetContextProperty((request.EndpointName, request.PropertyName, request.PropertyValue));

            return Task.FromResult(new Empty());
        }
    }
}
