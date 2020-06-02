using MyMessages.Messages;
using NServiceBus.Pipeline;
using NServiceBus.Testing;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.Tests
{
    public class Reply_Operation_Interceptor
    {
        [Test]
        public async Task Should_Capture_Reply_Message_Operation()
        {
            var scenarioContext = new IntegrationScenarioContext();
            var context = new TestableOutgoingReplyContext
            {
                Message = new OutgoingLogicalMessage(typeof(AMessage), new AMessage())
            };

            var sut = new InterceptReplyOperations("fake-endpoint", scenarioContext);
            await sut.Invoke(context, () => Task.CompletedTask).ConfigureAwait(false);

            var operation = scenarioContext.OutgoingMessageOperations.SingleOrDefault() as ReplyOperation;

            Assert.AreEqual(1, scenarioContext.OutgoingMessageOperations.Count());
            Assert.IsNotNull(operation);
        }
    }
}
