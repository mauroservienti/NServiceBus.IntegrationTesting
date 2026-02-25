using NServiceBus;
using SampleMessages;

namespace SampleEndpoint;

/// <summary>
/// Reusable endpoint configuration factory.
/// Used by Program.cs (production) and by SampleEndpoint.Testing (integration tests).
/// </summary>
public static class SampleEndpointConfig
{
    public static EndpointConfiguration Create()
    {
        var rabbitMqConnectionString =
            Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING")
            ?? "host=localhost;username=guest;password=guest";

        var endpointConfiguration = new EndpointConfiguration("SampleEndpoint");

        var transport = new RabbitMQTransport(
            RoutingTopology.Conventional(QueueType.Quorum),
            rabbitMqConnectionString);

        var routing = endpointConfiguration.UseTransport(transport);
        routing.RouteToEndpoint(typeof(SomeMessage), "SampleEndpoint");
        routing.RouteToEndpoint(typeof(AnotherMessage), "AnotherEndpoint");

        endpointConfiguration.UseSerialization<SystemJsonSerializer>();
        endpointConfiguration.EnableInstallers();

        return endpointConfiguration;
    }
}
