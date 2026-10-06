using NUnit.Framework;
using PublicApiGenerator;

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
    public void PublicApi_matches_approved_snapshot()
    {
        var publicApi = typeof(TestEnvironment).Assembly.GeneratePublicApi(Options);
        Approver.Verify(publicApi);
    }

    [Test]
    public void RabbitMQ_PublicApi_matches_approved_snapshot()
    {
        var publicApi = typeof(RabbitMqContainerOptions).Assembly.GeneratePublicApi(Options);
        Approver.Verify(publicApi);
    }

    [Test]
    public void PostgreSql_PublicApi_matches_approved_snapshot()
    {
        var publicApi = typeof(PostgreSqlContainerOptions).Assembly.GeneratePublicApi(Options);
        Approver.Verify(publicApi);
    }

    [Test]
    public void MySql_PublicApi_matches_approved_snapshot()
    {
        var publicApi = typeof(MySqlContainerOptions).Assembly.GeneratePublicApi(Options);
        Approver.Verify(publicApi);
    }

    [Test]
    public void MongoDb_PublicApi_matches_approved_snapshot()
    {
        var publicApi = typeof(MongoDbContainerOptions).Assembly.GeneratePublicApi(Options);
        Approver.Verify(publicApi);
    }

    [Test]
    public void SqlServer_PublicApi_matches_approved_snapshot()
    {
        var publicApi = typeof(SqlServerContainerOptions).Assembly.GeneratePublicApi(Options);
        Approver.Verify(publicApi);
    }

    [Test]
    public void RavenDb_PublicApi_matches_approved_snapshot()
    {
        var publicApi = typeof(RavenDbContainerOptions).Assembly.GeneratePublicApi(Options);
        Approver.Verify(publicApi);
    }
}
