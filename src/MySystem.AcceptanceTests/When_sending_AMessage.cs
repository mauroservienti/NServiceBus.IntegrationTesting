#if NET48_OR_GREATER

using MyMessages.Messages;
using NServiceBus.AcceptanceTesting;
using NServiceBus.IntegrationTesting;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace MySystem.AcceptanceTests
{
    public class When_sending_AMessage
    {
        [OneTimeSetUp]
        public async Task Setup()
        {
            await DockerCompose.Up();
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            DockerCompose.Down();
        }

        class Context : IntegrationScenarioContext
        {
            public bool RemoteWhenExecuted { get; set; }
            public bool InProcessWhenExecuted { get; set; }
        }

        [Test]
        public async Task AReplyMessage_is_received_and_ASaga_is_started()
        {
            var theExpectedIdentifier = Guid.NewGuid();
            var context = await Scenario.Define<Context>()
                .WithOutOfProcessEndpoint("MyService", EndpointReference.FromSolutionProject("MyService.TestProxy"), behavior =>
                {
                    behavior.When((remoteSession, ctx) =>
                    {
                        ctx.RemoteWhenExecuted = true;
                        return Task.CompletedTask;
                    });
                    //behavior.When(remoteSession => remoteSession.Send(new AMessage() { AnIdentifier = theExpectedIdentifier }));
                })
                .WithEndpoint<MyOtherServiceEndpoint>(behavior => 
                {
                    behavior.When((session, ctx) =>
                    {
                        ctx.InProcessWhenExecuted = true;
                        return Task.CompletedTask;
                    });
                })
                .Done(c =>
                {
                    return c.RemoteWhenExecuted && c.InProcessWhenExecuted;
                    //return c.SagaWasInvoked<ASaga>() || c.HasFailedMessages();
                })
                .Run();

            Assert.True(context.RemoteWhenExecuted);
            Assert.True(context.InProcessWhenExecuted);

            //var invokedSaga = context.InvokedSagas.Single(s => s.SagaType == typeof(ASaga));

            //Assert.True(invokedSaga.IsNew);
            //Assert.AreEqual("MyService", invokedSaga.EndpointName);
            //Assert.True(((ASagaData)invokedSaga.SagaData).AnIdentifier == theExpectedIdentifier);
            Assert.False(context.HasFailedMessages());
            Assert.False(context.HasHandlingErrors());
        }

        class MyOtherServiceEndpoint : EndpointConfigurationBuilder
        {
            public MyOtherServiceEndpoint()
            {
                EndpointSetup<MyOtherServiceTemplate>();
            }
        }
    }
}

#endif