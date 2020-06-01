using System;

namespace NServiceBus.IntegrationTesting
{
    public abstract class Invocation
    {
        public object Message { get; internal set; }
        public Type MessageType { get; internal set; }
        public Exception HandlingError { get; internal set; }
    }

    public class HandlerInvocation : Invocation
    {
        public Type HandlerType { get; internal set; }
    }

    public class SagaInvocation : Invocation
    {
        public Type SagaType { get; internal set; }
        public bool NotFound { get; internal set; }
        public bool IsNew { get; internal set; }
        public bool IsCompleted { get; internal set; }
        public IContainSagaData SagaData { get; internal set; }
    }
}
