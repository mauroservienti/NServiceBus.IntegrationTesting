using NServiceBus;

namespace MyOtherService
{
    public static class MyOtherServiceConfigurationBuilder
    {
        public static EndpointConfiguration Build(string endpointName)
        {
            var config = new EndpointConfiguration(endpointName);
            var scanner = config.AssemblyScanner();
            scanner.IncludeOnly("MyOtherService.dll", "MyMessages.dll");

            config.UseSerialization<NewtonsoftSerializer>();
            config.UsePersistence<LearningPersistence>();
            config.EnableInstallers();

            var transportConfig = config.UseTransport<LearningTransport>();

            config.SendFailedMessagesTo("error");

            config.Conventions()
                .DefiningMessagesAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages"))
                .DefiningEventsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Events"))
                .DefiningCommandsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Commands"));
            
            return config;
        }
    }
}
