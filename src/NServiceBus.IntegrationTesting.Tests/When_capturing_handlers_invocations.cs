using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NServiceBus.IntegrationTesting.Tests
{
    public class When_capturing_handlers_invocations
    {
        class TestMessage : IMessage
        {

        }

        interface IMessageInterface: IMessage
        {

        }

        class InheritedMessage : IMessageInterface
        {

        }

        class TestMessageHandler : IHandleMessages<TestMessage>
        {
            public Task Handle(TestMessage message, IMessageHandlerContext context)
            {
                throw new NotImplementedException();
            }
        }

        class IMessageInterfaceHandler : IHandleMessages<IMessageInterface>
        {
            public Task Handle(IMessageInterface message, IMessageHandlerContext context)
            {
                throw new NotImplementedException();
            }
        }

        [Test]
        public void Condition_should_match_type()
        {
            var scenarioContext = new IntegrationScenarioContext();
            scenarioContext.CaptureInvokedHandler(new HandlerInvocation()
            {
                Message = new TestMessage(),
                EndpointName = "fake-endpoint",
                HandlerType = typeof(TestMessageHandler),
                HandlingError = null,
                MessageType = typeof(TestMessage)
            });

            Assert.IsTrue(scenarioContext.HandlerWasInvoked<TestMessageHandler>());
            Assert.IsTrue(scenarioContext.MessageWasProcessedByHandler<TestMessage, TestMessageHandler>());
        }

        [Test]
        public void Condition_should_match_base_type()
        {
            var scenarioContext = new IntegrationScenarioContext();
            scenarioContext.CaptureInvokedHandler(new HandlerInvocation()
            {
                Message = new InheritedMessage(),
                EndpointName = "fake-endpoint",
                HandlerType = typeof(IMessageInterfaceHandler),
                HandlingError = null,
                MessageType = typeof(InheritedMessage)
            });

            Assert.IsTrue(scenarioContext.HandlerWasInvoked<IMessageInterfaceHandler>());
            Assert.IsTrue(scenarioContext.MessageWasProcessedByHandler<IMessageInterface, IMessageInterfaceHandler>());
        }
    }
}