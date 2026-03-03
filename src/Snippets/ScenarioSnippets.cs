using NServiceBus;
using NServiceBus.IntegrationTesting.Agent;
using SampleMessages;

namespace Snippets;

// begin-snippet: scenario
public class SomeMessageScenario : Scenario
{
    public override string Name => "SomeMessage";

    public override async Task Execute(IMessageSession session,
        Dictionary<string, string> args, CancellationToken cancellationToken = default)
        => await session.Send(new SomeMessage { Id = Guid.Parse(args["ID"]) });
}
// end-snippet
