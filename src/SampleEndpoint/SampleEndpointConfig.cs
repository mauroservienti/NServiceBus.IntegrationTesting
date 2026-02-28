using Npgsql;
using NpgsqlTypes;
using NServiceBus;
using NServiceBus.Persistence.Sql;
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

        var postgresConnectionString =
            Environment.GetEnvironmentVariable("POSTGRESQL_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

        var endpointConfiguration = new EndpointConfiguration("SampleEndpoint");

        var transport = new RabbitMQTransport(
            RoutingTopology.Conventional(QueueType.Quorum),
            rabbitMqConnectionString);

        var routing = endpointConfiguration.UseTransport(transport);
        routing.RouteToEndpoint(typeof(SomeMessage), "SampleEndpoint");
        routing.RouteToEndpoint(typeof(AnotherMessage), "AnotherEndpoint");
        routing.RouteToEndpoint(typeof(FailingMessage), "AnotherEndpoint");

        var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
        var dialect = persistence.SqlDialect<SqlDialect.PostgreSql>();
        dialect.JsonBParameterModifier(parameter =>
        {
            var npgsqlParameter = (NpgsqlParameter)parameter;
            npgsqlParameter.NpgsqlDbType = NpgsqlDbType.Jsonb;
        });
        persistence.ConnectionBuilder(() => new NpgsqlConnection(postgresConnectionString));

        endpointConfiguration.UseSerialization<SystemJsonSerializer>();
        endpointConfiguration.EnableInstallers();

        return endpointConfiguration;
    }
}
