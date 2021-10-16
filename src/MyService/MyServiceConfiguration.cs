using MyMessages.Messages;
using NServiceBus;

namespace MyService
{
    class MyServiceConfiguration : EndpointConfiguration
    {
        public MyServiceConfiguration()
            : base("MyService")
        {
            var scanner = this.AssemblyScanner();

            this.UseSerialization<NewtonsoftSerializer>();
            this.UsePersistence<LearningPersistence>();
            this.EnableInstallers();

            //TODO: renable when a compatible combination of alphas is available
            //var transport = new RabbitMQTransport(Topology.Conventional, "host=localhost;username=guest;password=guest");
            //var routing = this.UseTransport(transport);

            var routing = this.UseTransport(new LearningTransport());

            routing.RouteToEndpoint(typeof(AMessage), "MyOtherService");

            this.SendFailedMessagesTo("error");

            Conventions()
                .DefiningMessagesAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages"))
                .DefiningEventsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Events"))
                .DefiningCommandsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Commands"));
        }
    }
}
