using NServiceBus.IntegrationTesting;
using NUnit.Framework;

namespace NServiceBus.IntegrationTesting.Tests;

[TestFixture]
public class TestEnvironmentBuilderTests
{
    [Test]
    public void AddEndpoint_throws_on_duplicate_name()
    {
        var builder = new TestEnvironmentBuilder()
            .AddEndpoint("MyEndpoint", "MyEndpoint.Testing/Dockerfile");

        var ex = Assert.Throws<ArgumentException>(
            () => builder.AddEndpoint("MyEndpoint", "Other/Dockerfile"));

        Assert.That(ex!.ParamName, Is.EqualTo("endpointName"));
        Assert.That(ex.Message, Does.Contain("MyEndpoint"));
    }

    [Test]
    public void AddEndpoint_allows_different_names()
    {
        Assert.DoesNotThrow(() =>
            new TestEnvironmentBuilder()
                .AddEndpoint("EndpointA", "A/Dockerfile")
                .AddEndpoint("EndpointB", "B/Dockerfile"));
    }
}
