using MyMessages.Messages;
using NServiceBus.Pipeline;
using NServiceBus.Testing;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace NServiceBus.IntegrationTesting.Tests
{
    public class Publish_Operation_Interceptor
    {
        [Test]
        public async Task Should_Capture_Publish_Message_Operation()
        {
            var scenarioContext = new IntegrationScenarioContext();
            var context = new TestableOutgoingPublishContext
            {
                Message = new OutgoingLogicalMessage(typeof(AMessage), new AMessage())
            };

            var sut = new InterceptPublishOperations("fake-endpoint", scenarioContext);
            await sut.Invoke(context, () => Task.CompletedTask).ConfigureAwait(false);

            var operation = scenarioContext.OutgoingMessageOperations.SingleOrDefault() as PublishOperation;

            Assert.AreEqual(1, scenarioContext.OutgoingMessageOperations.Count());
            Assert.IsNotNull(operation);
        }
    }
}
