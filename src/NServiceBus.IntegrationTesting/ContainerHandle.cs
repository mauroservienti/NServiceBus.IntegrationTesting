using DotNet.Testcontainers.Containers;

namespace NServiceBus.IntegrationTesting;

/// <summary>
/// A handle to a named container registered with <see cref="TestEnvironmentBuilder.AddContainer"/>.
/// Provides port-mapping and log helpers for containers that do not host an NServiceBus agent.
/// </summary>
public sealed class ContainerHandle
{
    readonly IContainer _container;

    /// <summary>The name this container was registered under.</summary>
    public string Name { get; }

    internal ContainerHandle(string name, IContainer container)
    {
        Name = name;
        _container = container;
    }

    /// <summary>
    /// Returns the host-side port that Testcontainers mapped to
    /// <paramref name="containerPort"/>. The port must have been declared via
    /// <c>b.WithPortBinding(<paramref name="containerPort"/>, assignRandomHostPort: true)</c>
    /// in the <c>containerBuilder</c> callback passed to
    /// <see cref="TestEnvironmentBuilder.AddContainer"/>.
    /// </summary>
    public int GetMappedPort(int containerPort)
        => _container.GetMappedPublicPort(containerPort);

    /// <summary>
    /// Returns a base URL (<c>{scheme}://localhost:{mappedPort}</c>) for the given
    /// container port. The port must have been declared via
    /// <c>b.WithPortBinding(<paramref name="containerPort"/>, assignRandomHostPort: true)</c>
    /// in the <c>containerBuilder</c> callback passed to
    /// <see cref="TestEnvironmentBuilder.AddContainer"/>.
    /// </summary>
    public string GetBaseUrl(int containerPort, string scheme = "http")
        => $"{scheme}://localhost:{GetMappedPort(containerPort)}";

    /// <summary>
    /// Returns the stdout and stderr of this container.
    /// Useful for dumping diagnostic output when a test fails.
    /// </summary>
    public Task<(string Stdout, string Stderr)> GetLogsAsync()
        => _container.GetLogsAsync();
}
