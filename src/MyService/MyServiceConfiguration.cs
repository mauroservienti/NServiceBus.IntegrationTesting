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
            this.UseSerialization<NewtonsoftSerializer>();
            this.UsePersistence<LearningPersistence>();
            var transportConfig = this.UseTransport<LearningTransport>();

            var included = new[] { "MyService.dll", "MyMessages.dll" };
            var excluded = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll")
                .Select(path => Path.GetFileName(path))
                .Where(existingAssembly => !included.Contains(existingAssembly))
                .ToArray();

            var scanner = this.AssemblyScanner();
            scanner.ExcludeAssemblies(excluded);

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
