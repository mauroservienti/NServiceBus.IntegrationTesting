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

            config.UseTransport<LearningTransport>();

            return config;
        }
    }
    // end-snippet
}