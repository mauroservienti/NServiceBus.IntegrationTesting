namespace NServiceBus.IntegrationTesting.Agent;

/// <summary>
/// Defines a named test scenario that executes within the endpoint process.
/// Scenarios are defined in the SampleEndpoint.Testing project alongside the
/// endpoint bootstrap, giving them access to the real IMessageSession and all
/// message types — no cross-process serialization involved.
///
/// Register scenarios via IntegrationTestingBootstrap.RunAsync(..., [new MyScenario()]).
/// Trigger them from the test project via TestHostGrpcService.ExecuteScenarioAsync.
/// </summary>
public abstract class Scenario
{
    /// <summary>
    /// Unique name used to identify the scenario from the test project.
    /// Must match the name passed to ExecuteScenarioAsync.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Executes the scenario using the endpoint's real message session.
    /// The session is fully configured with the endpoint's serializer, routing,
    /// and transport — no separate serialization step.
    /// </summary>
    /// <param name="session">The live NServiceBus message session.</param>
    /// <param name="args">Optional named arguments passed from the test project.</param>
    public abstract Task Execute(
        IMessageSession session,
        Dictionary<string, string> args,
        CancellationToken cancellationToken = default);
}
