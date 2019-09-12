using MyMessages.Messages;
using NServiceBus;
using System;
using System.IO;
using System.Linq;

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
            var transportConfig = this.UseTransport<LearningTransport>();

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
