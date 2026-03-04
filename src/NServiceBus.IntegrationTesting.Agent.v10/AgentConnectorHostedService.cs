using Microsoft.Extensions.Hosting;

namespace NServiceBus.IntegrationTesting.Agent;

/// <summary>
/// Registered by
/// <see cref="IntegrationTestingBootstrap.Configure(string, EndpointConfiguration, Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Collections.Generic.IEnumerable{Scenario}?, System.Collections.Generic.IEnumerable{TimeoutRule}?, System.Collections.Generic.IEnumerable{SkipRule}?)"/>
/// to connect the agent to the test host after the NServiceBus hosted service has started
/// and registered <c>IMessageSession</c> in the DI container.
/// </summary>
sealed class AgentConnectorHostedService : IHostedService
{
    readonly AgentService _agent;
    readonly IServiceProvider _sp;

    public AgentConnectorHostedService(AgentService agent, IServiceProvider sp)
    {
        _agent = agent;
        _sp = sp;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var session = (IMessageSession)_sp.GetService(typeof(IMessageSession))!;
        return _agent.ConnectAsync(session, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => _agent.DisposeAsync().AsTask();
}
