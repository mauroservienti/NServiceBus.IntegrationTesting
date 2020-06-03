using System;
using System.Collections.Generic;

namespace NServiceBus.IntegrationTesting
{
    public abstract class OutgoingMessageOperation
    {
        public string SenderEndpoint { get; internal set; }
        public string MessageId { get; internal set; }
        public Type MessageType { get; internal set; }
        public object MessageInstance { get; internal set; }
        public Dictionary<string, string> MessageHeaders { get; internal set; }
        public Exception OperationError { get; internal set; }
    }

    public class SendOperation : OutgoingMessageOperation { }

    public class ReplyOperation : OutgoingMessageOperation { }

    public class PublishOperation : OutgoingMessageOperation { }

    public class RequestTimeoutOperation : SendOperation 
    {
        public string SagaId { get; internal set; }
        public string SagaTypeAssemblyQualifiedName { get; internal set; }
    }
}
