using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Microsoft.Extensions.Logging;
using NServiceBus.IntegrationTesting;
using NUnit.Framework;

namespace NServiceBus.IntegrationTesting.Tests;

/// <summary>
/// Unit tests for EndpointHandle.StopAsync and StartAsync.
/// Uses a FakeContainer to verify container interaction and connection-state
/// behaviour without requiring a real Docker daemon.
/// </summary>
[TestFixture]
public class EndpointHandleLifecycleTests
{
    const string EndpointName = "MyEndpoint";

    static EndpointHandle BuildHandle(TestHostGrpcService svc, FakeContainer container)
        => new(svc, EndpointName, container);

    // ── StopAsync ────────────────────────────────────────────────────────────

    [Test]
    public async Task StopAsync_stops_the_container()
    {
        var svc = new TestHostGrpcService();
        var container = new FakeContainer();
        var handle = BuildHandle(svc, container);

        await handle.StopAsync();

        Assert.That(container.StopCalled, Is.True);
    }

    [Test]
    public async Task StopAsync_resets_agent_connection_so_WaitForAgentAsync_blocks()
    {
        var svc = new TestHostGrpcService();
        var container = new FakeContainer();
        var handle = BuildHandle(svc, container);

        // Establish a completed connection.
        svc.SimulateAgentConnected(EndpointName);
        var connectedTask = svc.WaitForAgentAsync(EndpointName);
        Assert.That(connectedTask.IsCompletedSuccessfully, Is.True,
            "Baseline: WaitForAgentAsync must return immediately when already connected.");

        await handle.StopAsync();

        // After stop, the connection must be reset — WaitForAgentAsync must block.
        using var cts = new CancellationTokenSource(millisecondsDelay: 50);
        var waitAfterStop = svc.WaitForAgentAsync(EndpointName, cts.Token);
        Assert.That(waitAfterStop.IsCompleted, Is.False,
            "WaitForAgentAsync must block after StopAsync resets the agent connection.");

        await Task.Delay(100); // let the CTS expire cleanly
    }

    // ── StartAsync ───────────────────────────────────────────────────────────

    [Test]
    public async Task StartAsync_starts_the_container()
    {
        var svc = new TestHostGrpcService();
        var container = new FakeContainer();
        var handle = BuildHandle(svc, container);

        // Satisfy the WaitForAgentAsync inside StartAsync on a background thread.
        _ = Task.Run(async () =>
        {
            await Task.Delay(20);
            svc.SimulateAgentConnected(EndpointName);
        });

        await handle.StartAsync();

        Assert.That(container.StartCalled, Is.True);
    }

    [Test]
    public async Task StartAsync_waits_for_agent_to_reconnect()
    {
        var svc = new TestHostGrpcService();
        var container = new FakeContainer();
        var handle = BuildHandle(svc, container);

        var startTask = handle.StartAsync();
        Assert.That(startTask.IsCompleted, Is.False,
            "StartAsync must block until the agent connects.");

        svc.SimulateAgentConnected(EndpointName);
        await startTask;

        Assert.That(startTask.IsCompletedSuccessfully, Is.True,
            "StartAsync must complete once the agent connects.");
    }
}

/// <summary>
/// Minimal IContainer stub. Only StopAsync and StartAsync are meaningful;
/// all other members throw NotImplementedException or return harmless defaults.
/// </summary>
sealed class FakeContainer : IContainer
{
    public bool StopCalled { get; private set; }
    public bool StartCalled { get; private set; }

    public Task StopAsync(CancellationToken ct = default) { StopCalled = true; return Task.CompletedTask; }
    public Task StartAsync(CancellationToken ct = default) { StartCalled = true; return Task.CompletedTask; }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public event EventHandler? Creating  { add { } remove { } }
    public event EventHandler? Starting  { add { } remove { } }
    public event EventHandler? Stopping  { add { } remove { } }
    public event EventHandler? Pausing   { add { } remove { } }
    public event EventHandler? Unpausing { add { } remove { } }
    public event EventHandler? Created   { add { } remove { } }
    public event EventHandler? Started   { add { } remove { } }
    public event EventHandler? Stopped   { add { } remove { } }
    public event EventHandler? Paused    { add { } remove { } }
    public event EventHandler? Unpaused  { add { } remove { } }

    public DateTime CreatedTime  => default;
    public DateTime StartedTime  => default;
    public DateTime StoppedTime  => default;
    public DateTime PausedTime   => default;
    public DateTime UnpausedTime => default;
    public long HealthCheckFailingStreak => 0;
    public TestcontainersStates State => default;
    public TestcontainersHealthStatus Health => default;

    public ILogger Logger  => throw new NotImplementedException();
    public string Id       => throw new NotImplementedException();
    public string Name     => throw new NotImplementedException();
    public string IpAddress  => throw new NotImplementedException();
    public string MacAddress => throw new NotImplementedException();
    public string Hostname   => throw new NotImplementedException();
    public IImage Image      => throw new NotImplementedException();

    public ushort GetMappedPublicPort() => throw new NotImplementedException();
    public ushort GetMappedPublicPort(int containerPort) => throw new NotImplementedException();
    public ushort GetMappedPublicPort(string containerPort) => throw new NotImplementedException();
    public IReadOnlyDictionary<ushort, ushort> GetMappedPublicPorts() => throw new NotImplementedException();
    public Task<long> GetExitCodeAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<(string Stdout, string Stderr)> GetLogsAsync(DateTime since = default, DateTime until = default, bool timestampsEnabled = true, CancellationToken ct = default) => throw new NotImplementedException();
    public Task PauseAsync(CancellationToken ct = default)   => throw new NotImplementedException();
    public Task UnpauseAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task CopyAsync(byte[] fileContent, string filePath, uint uid = 0, uint gid = 0, DotNet.Testcontainers.Configurations.UnixFileModes fileMode = default, CancellationToken ct = default) => throw new NotImplementedException();
    public Task CopyAsync(string source, string target, uint uid = 0, uint gid = 0, DotNet.Testcontainers.Configurations.UnixFileModes fileMode = default, CancellationToken ct = default) => throw new NotImplementedException();
    public Task CopyAsync(DirectoryInfo source, string target, uint uid = 0, uint gid = 0, DotNet.Testcontainers.Configurations.UnixFileModes fileMode = default, CancellationToken ct = default) => throw new NotImplementedException();
    public Task CopyAsync(FileInfo source, string target, uint uid = 0, uint gid = 0, DotNet.Testcontainers.Configurations.UnixFileModes fileMode = default, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<byte[]> ReadFileAsync(string filePath, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<ExecResult> ExecAsync(IList<string> command, CancellationToken ct = default) => throw new NotImplementedException();
    public string GetConnectionString(ConnectionMode connectionMode) => throw new NotImplementedException();
    public string GetConnectionString(string key, ConnectionMode connectionMode) => throw new NotImplementedException();
}
