using NServiceBus;

namespace AnotherEndpoint;

public static class AnotherEndpointConfig
{
    public static EndpointConfiguration Create()
    {
        var rabbitMqConnectionString =
            Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING")
            ?? "host=localhost;username=guest;password=guest";

        var endpointConfiguration = new EndpointConfiguration("AnotherEndpoint");

        var transport = new RabbitMQTransport(
            RoutingTopology.Conventional(QueueType.Quorum),
            rabbitMqConnectionString);

        endpointConfiguration.UseTransport(transport);
        endpointConfiguration.UseSerialization<SystemJsonSerializer>();
        endpointConfiguration.EnableInstallers();

        return endpointConfiguration;
    }
}
