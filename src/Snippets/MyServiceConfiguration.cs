using NServiceBus;

namespace Snippets
{
    // begin-snippet: inherit-from-endpoint-configuration
    public class MyServiceConfiguration : EndpointConfiguration
    {
        public MyServiceConfiguration()
            : base("MyService")
        {
            this.SendFailedMessagesTo("error");
            this.EnableInstallers();

            var transportConfig = this.UseTransport<RabbitMQTransport>();
            transportConfig.UseConventionalRoutingTopology();
            transportConfig.ConnectionString("host=localhost");
        }
    }
    // end-snippet

    // begin-snippet: use-builder-class
    public static class MyServiceConfigurationBuilder
    {
        public static EndpointConfiguration Build(string endpointName, string rabbitMqConnectionString)
        {
            var config = new EndpointConfiguration(endpointName);
            config.SendFailedMessagesTo("error");
            config.EnableInstallers();

            var transportConfig = config.UseTransport<RabbitMQTransport>();
            transportConfig.UseConventionalRoutingTopology();
            transportConfig.ConnectionString(rabbitMqConnectionString);

            return config;
        }
    }
    // end-snippet
}