using NServiceBus;
using NServiceBus.IntegrationTesting.Agent;
using NServiceBus.Transport.RabbitMQ;
using Snippets.GettingStartedScenario;
using YourEndpoint.Messages;

namespace Snippets.GettingStartedConfig;

// begin-snippet: gs-config-factory
// YourEndpoint/YourEndpointConfig.cs
public static class YourEndpointConfig
{
    public static EndpointConfiguration Create()
    {
        var rabbitMqConnectionString =
            Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING")
            ?? "host=localhost;username=guest;password=guest";

        var config = new EndpointConfiguration("YourEndpoint");

        var transport = new RabbitMQTransport(
            RoutingTopology.Conventional(QueueType.Quorum),
            rabbitMqConnectionString);

        var routing = config.UseTransport(transport);
        routing.RouteToEndpoint(typeof(SomeCommand), "AnotherEndpoint");

        config.UseSerialization<SystemJsonSerializer>();
        config.EnableInstallers();

        return config;
    }
}
// end-snippet

public static class BootstrapSnippets
{
    public static async Task Bootstrap()
    {
        // begin-snippet: gs-bootstrap
        // YourEndpoint.Testing/Program.cs
        await IntegrationTestingBootstrap.RunAsync(
            "YourEndpoint",
            YourEndpointConfig.Create,
            scenarios: [new SomeCommandScenario()]);
        // end-snippet
    }

    public static async Task Overlay()
    {
        // begin-snippet: gs-overlay
        await IntegrationTestingBootstrap.RunAsync(
            "YourEndpoint",
            CreateConfig);

        static EndpointConfiguration CreateConfig()
        {
            var config = YourEndpointConfig.Create();
            config.Recoverability().Immediate(s => s.NumberOfRetries(0));
            config.Recoverability().Delayed(s => s.NumberOfRetries(0));
            return config;
        }
        // end-snippet
    }
}
