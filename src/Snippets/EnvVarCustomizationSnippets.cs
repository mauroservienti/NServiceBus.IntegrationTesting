using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using NServiceBus.IntegrationTesting;

namespace Snippets.EnvVarCustomization;

public class EnvVarCustomizationSnippets
{
    public async Task GlobalRabbitMq()
    {
        string srcDir = null!;

        // begin-snippet: env-var-global-rabbitmq
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMQ(opts => opts.ConnectionStringEnvVarName = "TRANSPORT_CONNECTION_STRING")
            .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
            .StartAsync();
        // end-snippet
    }

    public async Task GlobalPostgreSql()
    {
        string srcDir = null!;

        // begin-snippet: env-var-global-postgresql
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UsePostgreSql(opts => opts.ConnectionStringEnvVarName = "DB_CONNECTION_STRING")
            .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
            .StartAsync();
        // end-snippet
    }

    public async Task PerEndpointRabbitMq()
    {
        string srcDir = null!;

        // begin-snippet: env-var-per-endpoint-rabbitmq
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMQ()
            .AddEndpoint("TeamAEndpoint", "TeamA.Testing/Dockerfile", opts =>
                opts.InfrastructureEnvVarNames[RabbitMqContainerOptions.InfrastructureKey] = "RABBIT_CONNECTION_STRING")
            .AddEndpoint("TeamBEndpoint", "TeamB.Testing/Dockerfile", opts =>
                opts.InfrastructureEnvVarNames[RabbitMqContainerOptions.InfrastructureKey] = "TRANSPORT_CONNECTION_STRING")
            .StartAsync();
        // end-snippet
    }

    public async Task PerEndpointWireMock()
    {
        string srcDir = null!;

        // begin-snippet: env-var-per-endpoint-wiremock
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMQ()
            .UseWireMock()
            .AddEndpoint("TeamAEndpoint", "TeamA.Testing/Dockerfile", opts =>
                opts.InfrastructureEnvVarNames[WireMockOptions.InfrastructureKey] = "EXTERNAL_HTTP_STUB_URL")
            .AddEndpoint("TeamBEndpoint", "TeamB.Testing/Dockerfile", opts =>
                opts.InfrastructureEnvVarNames[WireMockOptions.InfrastructureKey] = "MOCK_SERVER_URL")
            .StartAsync();
        // end-snippet
    }

    public async Task PerEndpointAdditional()
    {
        string srcDir = null!;

        // begin-snippet: env-var-per-endpoint-additional
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMQ()
            .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile", opts =>
            {
                opts.EnvironmentVariables["FEATURE_FLAG_X"] = "true";
                opts.EnvironmentVariables["API_BASE_URL"] = "https://internal.example.com";
            })
            .StartAsync();
        // end-snippet
    }

    public async Task PerEndpointCombined()
    {
        string srcDir = null!;

        // begin-snippet: env-var-per-endpoint-combined
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMQ()
            .UsePostgreSql()
            .UseWireMock()
            .AddEndpoint("TeamAEndpoint", "TeamA.Testing/Dockerfile", opts =>
            {
                opts.InfrastructureEnvVarNames[RabbitMqContainerOptions.InfrastructureKey] = "RABBIT_CONNECTION_STRING";
                opts.InfrastructureEnvVarNames[PostgreSqlContainerOptions.InfrastructureKey] = "DB_CONNECTION_STRING";
                opts.InfrastructureEnvVarNames[WireMockOptions.InfrastructureKey] = "EXTERNAL_HTTP_STUB_URL";
                opts.EnvironmentVariables["FEATURE_FLAG_X"] = "true";
            })
            .AddEndpoint("TeamBEndpoint", "TeamB.Testing/Dockerfile", opts =>
            {
                opts.InfrastructureEnvVarNames[RabbitMqContainerOptions.InfrastructureKey] = "TRANSPORT_CONNECTION_STRING";
                opts.InfrastructureEnvVarNames[WireMockOptions.InfrastructureKey] = "MOCK_SERVER_URL";
            })
            .StartAsync();
        // end-snippet
    }

    public async Task CustomInfrastructure()
    {
        string srcDir = null!;

        // begin-snippet: env-var-use-infrastructure
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseInfrastructure(
                key: "redis",
                defaultEnvVarName: "REDIS_CONNECTION_STRING",
                buildContainer: network => new ContainerBuilder()
                    .WithImage("redis:7")
                    .WithNetwork(network)
                    .WithNetworkAliases("redis")
                    .Build(),
                connectionString: "redis:6379")
            .AddEndpoint("YourEndpoint", "YourEndpoint.Testing/Dockerfile")
            .StartAsync();
        // end-snippet
    }

    static TestEnvironment _env = null!;
}
