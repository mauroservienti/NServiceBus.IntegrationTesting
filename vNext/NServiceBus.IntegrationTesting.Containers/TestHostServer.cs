using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NServiceBus.IntegrationTesting.Containers;

/// <summary>
/// Hosts a gRPC server in the test process that endpoint agents connect back to.
/// Dispose to stop the server and release the port.
/// </summary>
public sealed class TestHostServer : IAsyncDisposable
{
    WebApplication? _app;

    /// <summary>The gRPC service that receives agent connections and events.</summary>
    public TestHostGrpcService GrpcService { get; } = new();

    /// <summary>The port the server is listening on (available after StartAsync).</summary>
    public int Port { get; private set; }

    /// <summary>Address for agents running as local processes.</summary>
    public string Address => $"http://localhost:{Port}";

    /// <summary>
    /// Address for agents running inside Docker containers.
    /// On Docker Desktop (macOS/Windows) host.docker.internal resolves to the host machine.
    /// </summary>
    public string ContainerAddress => $"http://host.docker.internal:{Port}";

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());

        // Port 0 = OS assigns a free port. Retrieve the actual port after start.
        // Bind to Any (0.0.0.0) so Docker containers can reach the test host.
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Any, 0, listenOptions =>
            {
                // gRPC requires HTTP/2.
                // h2c (HTTP/2 cleartext) is fine for local test traffic.
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });

        builder.Services.AddGrpc();
        builder.Services.AddSingleton(GrpcService);

        // Suppress most ASP.NET Core noise in test output.
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        _app = builder.Build();
        _app.MapGrpcService<TestHostGrpcService>();

        await _app.StartAsync();

        var addresses = _app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses;

        Port = new Uri(addresses.First()).Port;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
