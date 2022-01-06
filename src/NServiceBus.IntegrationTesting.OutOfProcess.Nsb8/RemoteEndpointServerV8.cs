using System;
using System.Collections.Generic;
using System.Text;

namespace NServiceBus.IntegrationTesting.OutOfProcess.Nsb8
{
    internal class RemoteEndpointServerV8 : RemoteEndpointServer
    {
        private IMessageSession _messageSession;

        public RemoteEndpointServerV8(string endpointName)
            : base(endpointName)
        {

        }

        public void RegisterMessageSession(IMessageSession messageSession)
        {
            _messageSession = messageSession;
        }
    }
}
