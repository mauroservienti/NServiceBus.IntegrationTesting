using MyMessages.Messages;
using NServiceBus.Pipeline;
using NServiceBus.Testing;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.Tests
{
    public class Send_Operation_Interceptor
    {
        [Test]
        public async Task Should_Capture_Sent_Message_Operation()
        {
            var integrationContext = new IntegrationScenarioContext();
            
            var sut = new InterceptSendOperations("fake-endpoint", integrationContext); ;
            var context = new TestableOutgoingSendContext
            {
                Message = new OutgoingLogicalMessage(typeof(AMessage), new AMessage())
            };

            await sut.Invoke(context, () => Task.CompletedTask).ConfigureAwait(false);

            Assert.AreEqual(1, integrationContext.OutgoingMessageOperations.Count());
        }
    }
}
