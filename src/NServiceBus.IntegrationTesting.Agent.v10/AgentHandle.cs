namespace NServiceBus.IntegrationTesting.Agent;

/// <summary>
/// A handle to the agent wired into a web application (or any host) that manages
/// its own NServiceBus endpoint lifecycle. Returned by
/// <see cref="IntegrationTestingBootstrap.Configure(string, EndpointConfiguration, System.Collections.Generic.IEnumerable{Scenario}?, System.Collections.Generic.IEnumerable{TimeoutRule}?, System.Collections.Generic.IEnumerable{SkipRule}?)"/>.
/// <para>
/// Call <see cref="ConnectAsync"/> after the NServiceBus endpoint has started —
/// typically from an <c>IHostedService.StartAsync</c> that runs after the NServiceBus
/// hosted service, or via the <c>IServiceCollection</c> overload of
/// <see cref="IntegrationTestingBootstrap.Configure(string, EndpointConfiguration, Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Collections.Generic.IEnumerable{Scenario}?, System.Collections.Generic.IEnumerable{TimeoutRule}?, System.Collections.Generic.IEnumerable{SkipRule}?)"/>
/// which registers the connection automatically.
/// </para>
/// </summary>
public sealed class AgentHandle : IAsyncDisposable
{
    internal AgentService AgentService { get; }

    internal AgentHandle(AgentService agentService) => AgentService = agentService;

    /// <summary>
    /// Connects the agent to the test host gRPC channel.
    /// Must be called after <c>Endpoint.Start()</c> (or after the NServiceBus hosted
    /// service has started when using the generic host).
    /// </summary>
    public Task ConnectAsync(IMessageSession session, CancellationToken cancellationToken = default)
        => AgentService.ConnectAsync(session, cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => AgentService.DisposeAsync();
}
