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
                return Task.CompletedTask;
            }
        }

        class MessageInterfaceHandler : IHandleMessages<IMessageInterface>
        {
            public Task Handle(IMessageInterface message, IMessageHandlerContext context)
            {
                return Task.CompletedTask;
            }
        }

        [Test]
        public void MessageWasProcessedByHandler_condition_should_match_type()
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

            Assert.That(scenarioContext.MessageWasProcessedByHandler<TestMessage, TestMessageHandler>(), Is.True);
        }

        [Test]
        public void MessageWasProcessedByHandler_condition_should_match_base_type()
        {
            var scenarioContext = new IntegrationScenarioContext();
            scenarioContext.CaptureInvokedHandler(new HandlerInvocation()
            {
                Message = new InheritedMessage(),
                EndpointName = "fake-endpoint",
                HandlerType = typeof(MessageInterfaceHandler),
                HandlingError = null,
                MessageType = typeof(InheritedMessage)
            });

            Assert.That(scenarioContext.MessageWasProcessedByHandler<IMessageInterface, MessageInterfaceHandler>(), Is.True);
        }

        [Test]
        public void MessageWasProcessed_condition_should_match_type()
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

            Assert.That(scenarioContext.MessageWasProcessed<TestMessage>(), Is.True);
        }

        [Test]
        public void MessageWasProcessed_condition_should_match_base_type()
        {
            var scenarioContext = new IntegrationScenarioContext();
            scenarioContext.CaptureInvokedHandler(new HandlerInvocation()
            {
                Message = new InheritedMessage(),
                EndpointName = "fake-endpoint",
                HandlerType = typeof(MessageInterfaceHandler),
                HandlingError = null,
                MessageType = typeof(InheritedMessage)
            });

            Assert.That(scenarioContext.MessageWasProcessed<IMessageInterface>(), Is.True);
        }
    }
}