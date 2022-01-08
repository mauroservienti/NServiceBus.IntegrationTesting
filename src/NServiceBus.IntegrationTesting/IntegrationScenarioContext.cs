using NServiceBus.AcceptanceTesting;
using NServiceBus.DelayedDelivery;
using NServiceBus.IntegrationTesting.OutOfProcess;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NServiceBus.IntegrationTesting
{
    public class IntegrationScenarioContext : ScenarioContext
    {
        static readonly object syncRoot = new object();
        readonly ConcurrentBag<HandlerInvocation> invokedHandlers = new();
        readonly ConcurrentBag<SagaInvocation> invokedSagas = new();
        readonly ConcurrentBag<OutgoingMessageOperation> outgoingMessageOperations = new();
        readonly ConcurrentBag<RemoteSendMessageOperation> remoteSendMessageOperations = new();
        readonly Dictionary<Type, Func<object, DoNotDeliverBefore, DoNotDeliverBefore>> timeoutRescheduleRules = new();
        readonly Dictionary<string, Dictionary<string, string>> properties = new();
        readonly Dictionary<string, (int runnerPort, int endpointPort)> ports = new();

        public IEnumerable<HandlerInvocation> InvokedHandlers { get { return invokedHandlers; } }

        internal (int runnerPort, int endpointPort) GetCommunicationPorts(string endpointName)
        {
            if (ports.Count == 0)
            {
                var p = (runnerPort: 30050, endpointPort: 40050);
                ports.Add(endpointName, p);

                return p;
            }

            if (ports.TryGetValue(endpointName, out var port)) 
            {
                return port;
            }

            var last = ports.Last();
            var newPorts = (runnerPort: last.Value.runnerPort + 1, endpointPort: last.Value.endpointPort + 1);
            ports.Add(endpointName, newPorts);

            return newPorts;
        }

        internal void AddRemoteOperation(RemoteSendMessageOperation operation)
        {
            remoteSendMessageOperations.Add(operation);
        }

        public IEnumerable<SagaInvocation> InvokedSagas { get { return invokedSagas; } }
        public IEnumerable<OutgoingMessageOperation> OutgoingMessageOperations { get { return outgoingMessageOperations; } }
        public IEnumerable<RemoteSendMessageOperation> RemoteSendMessageOperations { get { return remoteSendMessageOperations; } }

        public IReadOnlyDictionary<string, string> GetProperties(string endpointName)
        {
            lock (syncRoot)
            {
                if (!properties.TryGetValue(endpointName, out var endpointProps))
                {
                    endpointProps = new Dictionary<string, string>();
                }

                return endpointProps;
            }
        }

        internal HandlerInvocation CaptureInvokedHandler(HandlerInvocation invocation)
        {
            invokedHandlers.Add(invocation);

            return invocation;
        }

        public void RegisterTimeoutRescheduleRule<TTimeout>(Func<object, DoNotDeliverBefore, DoNotDeliverBefore> rule)
        {
            if (timeoutRescheduleRules.ContainsKey(typeof(TTimeout)))
            {
                throw new NotSupportedException("Only one rule per timeout message type is allowed.");
            }

            timeoutRescheduleRules.Add(typeof(TTimeout), rule);
        }

        internal bool TryGetTimeoutRescheduleRule(Type timeoutMessageType, out Func<object, DoNotDeliverBefore, DoNotDeliverBefore> rule)
        {
            return timeoutRescheduleRules.TryGetValue(timeoutMessageType, out rule);
        }

        internal void SetProperty(string endpointName, string propertyName, string propertyValue)
        {
            lock (syncRoot)
            {
                if (!properties.TryGetValue(endpointName, out var endpointProps))
                {
                    endpointProps = new Dictionary<string, string>();
                    properties[endpointName] = endpointProps;
                }

                endpointProps[propertyName] = propertyValue;
            }
        }

        internal void AddOutogingOperation(OutgoingMessageOperation outgoingMessageOperation)
        {
            outgoingMessageOperations.Add(outgoingMessageOperation);
        }

        internal SagaInvocation CaptureInvokedSaga(SagaInvocation invocation)
        {
            invokedSagas.Add(invocation);

            return invocation;
        }

        public bool HandlerWasInvoked<THandler>()
        {
            return InvokedHandlers.Any(invocation => invocation.HandlerType == typeof(THandler));
        }

        public bool SagaWasInvoked<TSaga>() where TSaga : Saga
        {
            return InvokedSagas.Any(invocation => invocation.SagaType == typeof(TSaga));
        }

        public bool SagaWasCompleted<TSaga>() where TSaga : Saga
        {
            return InvokedSagas.Any(invocation => invocation.SagaType == typeof(TSaga) && invocation.IsCompleted);
        }

        public bool MessageWasProcessed<TMessage>()
        {
            return invokedHandlers.Any(invocation => typeof(TMessage).IsAssignableFrom(invocation.MessageType))
                || invokedSagas.Any(invocation => typeof(TMessage).IsAssignableFrom(invocation.MessageType));
        }

        public bool MessageWasProcessedBySaga<TMessage, TSaga>()
        {
            return invokedSagas.Any(i => i.SagaType == typeof(TSaga) && typeof(TMessage).IsAssignableFrom(i.MessageType));
        }

        public bool MessageWasProcessedByHandler<TMessage, THandler>()
        {
            return invokedHandlers.Any(i => i.HandlerType == typeof(THandler) && typeof(TMessage).IsAssignableFrom(i.MessageType));
        }

        public bool HasFailedMessages()
        {
            return !FailedMessages.IsEmpty;
        }

        public bool HasHandlingErrors()
        {
            return invokedHandlers.Any(invocation => invocation.HandlingError != null)
                || invokedSagas.Any(invocation => invocation.HandlingError != null);
        }
    }
}
