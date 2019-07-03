using NServiceBus;

namespace MyOtherService
{
    public class MyOtherServiceConfiguration : EndpointConfiguration
    {
        public MyOtherServiceConfiguration()
            : base("MyOtherService")
        {
            this.UseSerialization<NewtonsoftSerializer>();
            var transportConfig = this.UseTransport<LearningTransport>();

            //transportConfig.Routing();

            this.SendFailedMessagesTo("error");

            Conventions()
                .DefiningMessagesAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages"))
                .DefiningEventsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Events"))
                .DefiningCommandsAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages.Commands"));
        }
    }
}
