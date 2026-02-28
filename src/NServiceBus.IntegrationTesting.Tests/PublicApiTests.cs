using PublicApiGenerator;
using VerifyNUnit;

namespace NServiceBus.IntegrationTesting.Tests;

[TestFixture]
public class PublicApiTests
{
    static ApiGeneratorOptions Options => new()
    {
        // Exclude framework-generated attributes that vary by TFM or SDK version
        // to keep the snapshot stable across net8 / net9 / net10 builds.
        ExcludeAttributes =
        [
            "System.Runtime.Versioning.TargetFrameworkAttribute",
            "System.Reflection.AssemblyMetadataAttribute"
        ]
    };

    [Test]
    public Task PublicApi_matches_approved_snapshot()
    {
        var publicApi = typeof(TestEnvironment).Assembly.GeneratePublicApi(Options);
        return Verifier.Verify(publicApi);
    }

    [Test]
    public Task RabbitMQ_PublicApi_matches_approved_snapshot()
    {
        var publicApi = typeof(RabbitMqContainerOptions).Assembly.GeneratePublicApi(Options);
        return Verifier.Verify(publicApi);
    }

    [Test]
    public Task PostgreSql_PublicApi_matches_approved_snapshot()
    {
        var publicApi = typeof(PostgreSqlContainerOptions).Assembly.GeneratePublicApi(Options);
        return Verifier.Verify(publicApi);
    }
}
