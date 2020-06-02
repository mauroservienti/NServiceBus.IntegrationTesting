using MyMessages.Messages;
using NServiceBus;

namespace MyService
{
    public class MyServiceConfiguration : EndpointConfiguration
    {
        public MyServiceConfiguration()
            : base("MyService")
        {
            var scanner = this.AssemblyScanner();
            scanner.IncludeOnly("MyService.dll", "MyMessages.dll");

            this.UseSerialization<NewtonsoftSerializer>();
            this.UsePersistence<LearningPersistence>();
            this.EnableInstallers();

            var transportConfig = this.UseTransport<RabbitMQTransport>();
            transportConfig.UseConventionalRoutingTopology();
            transportConfig.ConnectionString("host=localhost;username=guest;password=guest");

            transportConfig.Routing()
                .RouteToEndpoint(typeof(AMessage), "MyOtherService");

            this.SendFailedMessagesTo("error");

            Conventions()
                .DefiningMessagesAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages"))
                .DefiningEventsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Events"))
                .DefiningCommandsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Commands"));
        }
    }
}
