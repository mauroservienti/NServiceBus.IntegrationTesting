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
            scanner.IncludeOnly("MyService.dll", "MyMessages.dll");

            this.UseSerialization<SystemJsonSerializer>();
            this.UsePersistence<LearningPersistence>();
            this.EnableInstallers();

            var transport = new RabbitMQTransport(
                RoutingTopology.Conventional(QueueType.Quorum),
                "host=localhost;username=guest;password=guest");
            var routing = this.UseTransport(transport);

            routing.RouteToEndpoint(typeof(AMessage), "MyOtherService");

            this.SendFailedMessagesTo("error");

            Conventions()
                .DefiningMessagesAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages"))
                .DefiningEventsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Events"))
                .DefiningCommandsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Commands"));
        }
    }
}
