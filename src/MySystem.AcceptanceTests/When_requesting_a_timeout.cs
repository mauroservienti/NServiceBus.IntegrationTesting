using MyMessages.Messages;
using MyService;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.DelayedDelivery;
using NServiceBus.IntegrationTesting;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace MySystem.AcceptanceTests
{
    public class When_requesting_a_timeout
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
        public async Task It_should_be_rescheduled_and_handled()
        {
            var context = await Scenario.Define<IntegrationScenarioContext>(ctx =>
            {
                ctx.RegisterTimeoutRescheduleRule<ASaga.MyTimeout>((msg, delay) => new DoNotDeliverBefore(DateTime.UtcNow.AddSeconds(5)));
            })
                .WithGenericHostEndpoint("MyService", configPreview => Program.CreateHostBuilder(new string[0], configPreview).Build(), behavior =>
            {
                behavior.When(session => session.Send("MyService", new StartASaga() {AnIdentifier = Guid.NewGuid()}));
            })
            .Done(ctx => ctx.MessageWasProcessedBySaga<ASaga.MyTimeout, ASaga>() || ctx.HasFailedMessages())
            .Run();

            Assert.That(context.MessageWasProcessedBySaga<ASaga.MyTimeout, ASaga>(), Is.True);
            Assert.That(context.HasFailedMessages(), Is.False);
            Assert.That(context.HasHandlingErrors(), Is.False);
        }
    }
}
