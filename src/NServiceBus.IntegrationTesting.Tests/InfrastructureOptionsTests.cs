using NServiceBus.IntegrationTesting;
using NUnit.Framework;

namespace NServiceBus.IntegrationTesting.Tests;

/// <summary>
/// Tests for infrastructure options classes covering:
/// - NetworkAlias defaults to Key
/// - Key drives ConnectionStringEnvVarName derivation
/// - Key and NetworkAlias validation
/// </summary>
[TestFixture]
public class InfrastructureOptionsTests
{
    // -------------------------------------------------------------------------
    // NetworkAlias defaults to Key (covers the two classes that changed
    // meaningfully, plus one representative from the unchanged group)
    // -------------------------------------------------------------------------

    [Test]
    public void PostgreSql_NetworkAlias_defaults_to_Key()
    {
        var opts = new PostgreSqlContainerOptions();
        Assert.That(opts.NetworkAlias, Is.EqualTo(opts.Key));
        Assert.That(opts.NetworkAlias, Is.EqualTo("postgresql"));
    }

    [Test]
    public void SqlServer_NetworkAlias_defaults_to_Key()
    {
        var opts = new SqlServerContainerOptions();
        Assert.That(opts.NetworkAlias, Is.EqualTo(opts.Key));
        Assert.That(opts.NetworkAlias, Is.EqualTo("sqlserver"));
    }

    [Test]
    public void RabbitMq_NetworkAlias_defaults_to_Key()
    {
        var opts = new RabbitMqContainerOptions();
        Assert.That(opts.NetworkAlias, Is.EqualTo(opts.Key));
        Assert.That(opts.NetworkAlias, Is.EqualTo("rabbitmq"));
    }

    [Test]
    public void MySql_NetworkAlias_defaults_to_Key()
    {
        var opts = new MySqlContainerOptions();
        Assert.That(opts.NetworkAlias, Is.EqualTo(opts.Key));
    }

    [Test]
    public void MongoDb_NetworkAlias_defaults_to_Key()
    {
        var opts = new MongoDbContainerOptions();
        Assert.That(opts.NetworkAlias, Is.EqualTo(opts.Key));
    }

    [Test]
    public void RavenDb_NetworkAlias_defaults_to_Key()
    {
        var opts = new RavenDbContainerOptions();
        Assert.That(opts.NetworkAlias, Is.EqualTo(opts.Key));
    }

    // -------------------------------------------------------------------------
    // Changing Key changes the NetworkAlias default (uses PostgreSQL as
    // representative — all classes share the same backing-field pattern)
    // -------------------------------------------------------------------------

    [Test]
    public void NetworkAlias_follows_Key_when_not_explicitly_set()
    {
        var opts = new PostgreSqlContainerOptions { Key = "postgresql-2" };
        Assert.That(opts.NetworkAlias, Is.EqualTo("postgresql-2"));
    }

    [Test]
    public void Explicit_NetworkAlias_is_preserved_after_Key_change()
    {
        var opts = new PostgreSqlContainerOptions();
        opts.NetworkAlias = "my-alias";
        opts.Key = "postgresql-2";
        Assert.That(opts.NetworkAlias, Is.EqualTo("my-alias"));
    }

    // -------------------------------------------------------------------------
    // ConnectionStringEnvVarName derivation
    // -------------------------------------------------------------------------

    [Test]
    public void ConnectionStringEnvVarName_derives_from_Key()
    {
        var opts = new PostgreSqlContainerOptions();
        Assert.That(opts.ConnectionStringEnvVarName, Is.EqualTo("POSTGRESQL_CONNECTION_STRING"));
    }

    [Test]
    public void ConnectionStringEnvVarName_updates_when_Key_changes()
    {
        var opts = new PostgreSqlContainerOptions { Key = "postgresql-primary" };
        Assert.That(opts.ConnectionStringEnvVarName, Is.EqualTo("POSTGRESQL_PRIMARY_CONNECTION_STRING"));
    }

    [Test]
    public void ConnectionStringEnvVarName_replaces_hyphens_with_underscores()
    {
        var opts = new PostgreSqlContainerOptions { Key = "my-db" };
        Assert.That(opts.ConnectionStringEnvVarName, Is.EqualTo("MY_DB_CONNECTION_STRING"));
    }

    [Test]
    public void Explicit_ConnectionStringEnvVarName_is_preserved_after_Key_change()
    {
        var opts = new PostgreSqlContainerOptions();
        opts.ConnectionStringEnvVarName = "MY_CUSTOM_VAR";
        opts.Key = "postgresql-2";
        Assert.That(opts.ConnectionStringEnvVarName, Is.EqualTo("MY_CUSTOM_VAR"));
    }

    // -------------------------------------------------------------------------
    // Key validation
    // -------------------------------------------------------------------------

    [TestCase("valid")]
    [TestCase("valid-key")]
    [TestCase("valid123")]
    [TestCase("a")]
    [TestCase("abc-123-def")]
    public void Key_accepts_valid_values(string key)
    {
        Assert.DoesNotThrow(() => new PostgreSqlContainerOptions { Key = key });
    }

    [TestCase("", Description = "empty string")]
    [TestCase("UPPER", Description = "uppercase letters")]
    [TestCase("with_underscore", Description = "underscore not allowed")]
    [TestCase("-leading", Description = "leading hyphen")]
    [TestCase("trailing-", Description = "trailing hyphen")]
    [TestCase("has space", Description = "space not allowed")]
    public void Key_rejects_invalid_values(string key)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new PostgreSqlContainerOptions { Key = key });
        Assert.That(ex!.ParamName, Is.EqualTo("value"));
    }

    // -------------------------------------------------------------------------
    // NetworkAlias validation
    // -------------------------------------------------------------------------

    [TestCase("valid")]
    [TestCase("valid-alias")]
    [TestCase("alias123")]
    public void NetworkAlias_accepts_valid_values(string alias)
    {
        Assert.DoesNotThrow(() => new PostgreSqlContainerOptions { NetworkAlias = alias });
    }

    [TestCase("", Description = "empty string")]
    [TestCase("UPPER", Description = "uppercase letters")]
    [TestCase("with_underscore", Description = "underscore not allowed")]
    [TestCase("-leading", Description = "leading hyphen")]
    [TestCase("trailing-", Description = "trailing hyphen")]
    public void NetworkAlias_rejects_invalid_values(string alias)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new PostgreSqlContainerOptions { NetworkAlias = alias });
        Assert.That(ex!.ParamName, Is.EqualTo("value"));
    }
}
