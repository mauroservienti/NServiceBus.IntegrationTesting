﻿using NServiceBus;

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

            //TODO: renable when a compatible combination of alphas is available
            //var transport = new RabbitMQTransport(Topology.Conventional, rabbitMqConnectionString);
            //config.UseTransport(transport);
            config.UseTransport(new LearningTransport());

            config.SendFailedMessagesTo("error");

            config.Conventions()
                .DefiningMessagesAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages"))
                .DefiningEventsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Events"))
                .DefiningCommandsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Commands"));

            return config;
        }
    }
}
