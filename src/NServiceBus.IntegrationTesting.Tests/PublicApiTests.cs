using PublicApiGenerator;
using VerifyNUnit;

namespace NServiceBus.IntegrationTesting.Tests;

[TestFixture]
public class PublicApiTests
{
    [Test]
    public Task PublicApi_matches_approved_snapshot()
    {
        var options = new ApiGeneratorOptions
        {
            // Exclude framework-generated attributes that vary by TFM or SDK version
            // to keep the snapshot stable across net8 / net9 / net10 builds.
            ExcludeAttributes =
            [
                "System.Runtime.Versioning.TargetFrameworkAttribute",
                "System.Reflection.AssemblyMetadataAttribute"
            ]
        };

        var publicApi = typeof(TestEnvironment).Assembly.GeneratePublicApi(options);
        return Verifier.Verify(publicApi);
    }
}
