using System;
using System.Collections.Generic;

namespace NServiceBus.IntegrationTesting
{
    public abstract class RemoteOutgoingMessageOperation
    {
        public string SenderEndpoint { get; set; }
        public string MessageId { get; set; }
        public string MessageTypeAssemblyQualifiedName { get; set; }
        public string MessageInstanceJson { get; set; }
        public Dictionary<string, string> MessageHeaders { get; set; }
        public string OperationErrorJson { get; set; }
        public string OperationErrorTypeAssemblyQualifiedName { get; set; }
    }

    public class RemoteSendOperation : RemoteOutgoingMessageOperation { }

    public class RemoteReplyOperation : RemoteOutgoingMessageOperation { }

    public class RemotePublishOperation : RemoteOutgoingMessageOperation { }

    public class RemoteRequestTimeoutOperation : RemoteSendOperation
    {
        public string SagaId { get; set; }
        public string SagaTypeAssemblyQualifiedName { get; set; }
    }
}
