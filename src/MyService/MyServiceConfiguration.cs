﻿using MyMessages.Messages;
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

            var transport = new LearningTransport();
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
