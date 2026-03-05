using DotNet.Testcontainers.Containers;

namespace NServiceBus.IntegrationTesting;

/// <summary>
/// A handle to a named infrastructure container (e.g. RabbitMQ, PostgreSQL).
/// Provides lifecycle control and an <see cref="ExecAsync"/> escape hatch for
/// running arbitrary commands inside the container — useful for resetting state
/// between tests (e.g. truncating database tables, purging queues).
/// </summary>
public sealed class InfrastructureHandle
{
    readonly IContainer _container;

    /// <summary>The key this infrastructure container was registered under.</summary>
    public string Key { get; }

    internal InfrastructureHandle(string key, IContainer container)
    {
        Key = key;
        _container = container;
    }

    /// <summary>
    /// Executes a command inside the container and returns the result.
    /// Use this to reset infrastructure state between tests, for example:
    /// <code>
    /// await handle.ExecAsync(["psql", "-U", "postgres", "-c", "TRUNCATE orders"]);
    /// </code>
    /// </summary>
    public Task<ExecResult> ExecAsync(IList<string> command, CancellationToken cancellationToken = default)
        => _container.ExecAsync(command, cancellationToken);

    /// <summary>
    /// Stops this infrastructure container.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
        => _container.StopAsync(cancellationToken);

    /// <summary>
    /// Starts a previously stopped infrastructure container.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
        => _container.StartAsync(cancellationToken);

    /// <summary>
    /// Stops and restarts this infrastructure container.
    /// Use this to simulate a broker or database restart during chaos / resilience tests.
    /// </summary>
    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        await _container.StopAsync(cancellationToken);
        await _container.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the stdout and stderr of this container.
    /// Useful for dumping diagnostic output when a test fails.
    /// </summary>
    public Task<(string Stdout, string Stderr)> GetLogsAsync()
        => _container.GetLogsAsync();
}
