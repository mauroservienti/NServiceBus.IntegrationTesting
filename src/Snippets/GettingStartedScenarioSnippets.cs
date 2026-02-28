using NServiceBus;
using NServiceBus.IntegrationTesting.Agent;
using YourEndpoint.Messages;

namespace Snippets.GettingStartedScenario;

// begin-snippet: gs-scenario
// YourEndpoint.Testing/SomeCommandScenario.cs
public class SomeCommandScenario : Scenario
{
    public override string Name => "SomeCommand Scenario";

    public override async Task Execute(
        IMessageSession session,
        Dictionary<string, string> args,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.Parse(args["ID"]);
        await session.Send(new SomeCommand { Id = id });
    }
}
// end-snippet
