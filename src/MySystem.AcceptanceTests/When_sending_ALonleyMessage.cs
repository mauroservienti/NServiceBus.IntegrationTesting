using MyMessages.Messages;
using NServiceBus.AcceptanceTesting;
using NServiceBus.IntegrationTesting;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MySystem.AcceptanceTests
{
    public class When_sending_ALonleyMessage_out_of_process
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

        [Test]
        public async Task The_message_is_sent_as_expected()
        {
            var theExpectedIdentifier = Guid.NewGuid();
            var context = await Scenario.Define<IntegrationScenarioContext>()
                .WithOutOfProcessEndpoint("MyService", EndpointReference.FromSolutionProject("MyService.TestProxy"), behavior =>
                {
                    behavior.When(remoteSession => remoteSession.Send("MyService", new ALonleyMessage() { AnIdentifier = theExpectedIdentifier }));
                })
                .Done(c =>
                {
                    return c.OutgoingMessageOperations.Any(op => op.MessageType == typeof(ALonleyMessage)) || c.HasFailedMessages();
                })
                .Run();

            //Assert.True(context.MessageWasProcessed<ALonleyMessage>());
            
            Assert.False(context.HasFailedMessages());
            Assert.False(context.HasHandlingErrors());
        }
    }
}