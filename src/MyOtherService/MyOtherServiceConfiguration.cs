using NServiceBus;
using System;
using System.IO;
using System.Linq;

namespace MyOtherService
{
    public class MyOtherServiceConfiguration : EndpointConfiguration
    {
        public MyOtherServiceConfiguration()
            : base("MyOtherService")
        {
            this.UseSerialization<NewtonsoftSerializer>();
            this.UsePersistence<LearningPersistence>();
            var transportConfig = this.UseTransport<LearningTransport>();

            var included = new[] { "MyOtherService.dll", "MyMessages.dll" };
            var excluded = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll")
                .Select(path => Path.GetFileName(path))
                .Where(existingAssembly => !included.Contains(existingAssembly))
                .ToArray();

            var scanner = this.AssemblyScanner();
            scanner.ExcludeAssemblies(excluded);

            this.SendFailedMessagesTo("error");

            Conventions()
                .DefiningMessagesAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages"))
                .DefiningEventsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Events"))
                .DefiningCommandsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Commands"));
        }
    }
}
