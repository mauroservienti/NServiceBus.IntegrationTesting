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

    [Test]
    public void FindRootByDirectory_finds_git_root()
    {
        var root = TestEnvironmentBuilder.FindRootByDirectory(".git");
        Assert.That(Directory.Exists(Path.Combine(root, ".git")));
    }

    [Test]
    public void FindRootByDirectory_appends_subPath()
    {
        var root = TestEnvironmentBuilder.FindRootByDirectory(".git");
        var path = TestEnvironmentBuilder.FindRootByDirectory(".git", "src");
        Assert.That(path, Is.EqualTo(Path.Combine(root, "src")));
    }

    [Test]
    public void FindRootByDirectory_throws_when_not_found()
    {
        Assert.Throws<DirectoryNotFoundException>(
            () => TestEnvironmentBuilder.FindRootByDirectory("__nonexistent_marker__"));
    }

    [Test]
    public void FindRootByFile_finds_slnx_file()
    {
        var root = TestEnvironmentBuilder.FindRootByFile("*.slnx");
        Assert.That(Directory.GetFiles(root, "*.slnx"), Is.Not.Empty);
    }

    [Test]
    public void FindRootByFile_appends_subPath()
    {
        var root = TestEnvironmentBuilder.FindRootByFile("*.slnx");
        var path = TestEnvironmentBuilder.FindRootByFile("*.slnx", "bin");
        Assert.That(path, Is.EqualTo(Path.Combine(root, "bin")));
    }

    [Test]
    public void FindRootByFile_throws_when_not_found()
    {
        Assert.Throws<FileNotFoundException>(
            () => TestEnvironmentBuilder.FindRootByFile("__nonexistent__.xyz"));
    }
}
