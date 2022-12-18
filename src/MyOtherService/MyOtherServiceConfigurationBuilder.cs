using NServiceBus;

namespace MyOtherService
{
    public static class MyOtherServiceConfigurationBuilder
    {
        public static EndpointConfiguration Build(string endpointName, string rabbitMqConnectionString)
        {
            var config = new EndpointConfiguration(endpointName);
            var scanner = config.AssemblyScanner();
            scanner.IncludeOnly("MyOtherService.dll", "MyMessages.dll");

            config.UseSerialization<NewtonsoftJsonSerializer>();
            config.UsePersistence<LearningPersistence>();
            config.EnableInstallers();

            var transport = new RabbitMQTransport(
                RoutingTopology.Conventional(QueueType.Quorum),
                "host=localhost;username=guest;password=guest");
            var routing = config.UseTransport(transport);

            config.SendFailedMessagesTo("error");

            config.Conventions()
                .DefiningMessagesAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages"))
                .DefiningEventsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Events"))
                .DefiningCommandsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Commands"));

            return config;
        }
    }
}
