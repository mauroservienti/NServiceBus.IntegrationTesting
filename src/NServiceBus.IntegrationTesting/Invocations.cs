using System;

namespace NServiceBus.IntegrationTesting
{
    public abstract class Invocation
    {
        public Type MessageType { get; set; }
        public Exception HandlingError { get; internal set; }
    }

    public class HandlerInvocation : Invocation
    {
        public Type HandlerType { get; set; }
    }

    public class SagaInvocation : Invocation
    {
        public Type SagaType { get; set; }
        public bool NotFound { get; set; }
        public bool IsNew { get; set; }
        public IContainSagaData SagaData { get; internal set; }
    }
}
