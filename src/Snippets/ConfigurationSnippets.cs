using NServiceBus;

namespace ConfigurationSnippets
{
    // begin-snippet: inherit-from-endpoint-configuration
    public class MyServiceConfiguration : EndpointConfiguration
    {
        public MyServiceConfiguration()
            : base("MyService")
        {
            this.SendFailedMessagesTo("error");
            this.EnableInstallers();

            /*
             * Any NServiceBus suppported transport can be used. Tests in this 
             * repostory are using the LearningTransport for the setup simplicity
             */
            this.UseTransport<LearningTransport>();
        }
    }
    // end-snippet

    // begin-snippet: use-builder-class
    public static class MyServiceConfigurationBuilder
    {
        public static EndpointConfiguration Build(string endpointName)
        {
            var config = new EndpointConfiguration(endpointName);
            config.SendFailedMessagesTo("error");
            config.EnableInstallers();

            /*
             * Any NServiceBus suppported transport can be used. Tests in this 
             * repostory are using the LearningTransport for the setup simplicity
             */
            config.UseTransport<LearningTransport>();

            return config;
        }
    }
    // end-snippet
}