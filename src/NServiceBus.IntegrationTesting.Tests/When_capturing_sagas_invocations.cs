using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NServiceBus.IntegrationTesting.Tests
{
    public class When_capturing_sagas_invocations
    {
        class TestMessage : IMessage
        {
        }

        interface IMessageInterface : IMessage
        {
        }

        class InheritedMessage : IMessageInterface
        {
        }

        class TestSaga : Saga<TestSaga.SagaData>,
            IHandleMessages<TestMessage>,
            IHandleMessages<IMessageInterface>
        {
            internal class SagaData : ContainSagaData
            {
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaData> mapper)
            {
                /**/
            }

            public Task Handle(TestMessage message, IMessageHandlerContext context)
            {
                return Task.CompletedTask;
            }

            public Task Handle(IMessageInterface message, IMessageHandlerContext context)
            {
                return Task.CompletedTask;
            }
        }

        [Test]
        public void MessageWasProcessedBySaga_condition_should_match_type()
        {
            var scenarioContext = new IntegrationScenarioContext();
            scenarioContext.CaptureInvokedSaga(new SagaInvocation()
            {
                Message = new TestMessage(), EndpointName = "fake-endpoint", SagaType = typeof(TestSaga), MessageType = typeof(TestMessage)
            });

            Assert.IsTrue(scenarioContext.MessageWasProcessedBySaga<TestMessage, TestSaga>());
        }

        [Test]
        public void MessageWasProcessedBySaga_condition_should_match_base_type()
        {
            var scenarioContext = new IntegrationScenarioContext();
            scenarioContext.CaptureInvokedSaga(new SagaInvocation()
            {
                Message = new InheritedMessage(), EndpointName = "fake-endpoint", SagaType = typeof(TestSaga), MessageType = typeof(InheritedMessage)
            });

            Assert.IsTrue(scenarioContext.MessageWasProcessedBySaga<IMessageInterface, TestSaga>());
        }

        [Test]
        public void MessageWasProcessed_condition_should_match_type()
        {
            var scenarioContext = new IntegrationScenarioContext();
            scenarioContext.CaptureInvokedSaga(new SagaInvocation()
            {
                Message = new TestMessage(),
                EndpointName = "fake-endpoint",
                SagaType = typeof(TestSaga),
                HandlingError = null,
                MessageType = typeof(TestMessage)
            });

            Assert.IsTrue(scenarioContext.MessageWasProcessed<TestMessage>());
        }

        [Test]
        public void MessageWasProcessed_condition_should_match_base_type()
        {
            var scenarioContext = new IntegrationScenarioContext();
            scenarioContext.CaptureInvokedSaga(new SagaInvocation()
            {
                Message = new InheritedMessage(),
                EndpointName = "fake-endpoint",
                SagaType = typeof(TestSaga),
                HandlingError = null,
                MessageType = typeof(InheritedMessage)
            });

            Assert.IsTrue(scenarioContext.MessageWasProcessed<IMessageInterface>());
        }
    }
}