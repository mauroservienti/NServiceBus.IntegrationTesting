using MyMessages.Messages;
using NServiceBus.AcceptanceTesting;
using NServiceBus.IntegrationTesting;
using NServiceBus.IntegrationTesting.OutOfProcess;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MySystem.AcceptanceTests
{
    public class When_sending_ALonleyMessage_out_of_process
    {
        [Test]
        public async Task The_message_is_sent_as_expected()
        {
            const string EndpointName = "MyService";

            var theExpectedIdentifier = Guid.NewGuid();
            var context = await Scenario.Define<IntegrationScenarioContext>()
                .WithOutOfProcessEndpoint(EndpointName, EndpointReference.FromSolutionProject("MyService.TestProxy"), behavior =>
                {
                    behavior.When(
                        //condition: ctx => 
                        //{
                        //    return ctx.GetProperties(EndpointName).ContainsKey(Properties.DebuggerAttached);
                        //}, 
                        action: remoteSession =>
                        {
                            return remoteSession.Send(EndpointName, new ALonleyMessage() { AnIdentifier = theExpectedIdentifier });
                        });
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