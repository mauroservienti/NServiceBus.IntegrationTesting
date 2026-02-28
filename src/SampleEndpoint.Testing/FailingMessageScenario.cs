using NServiceBus;
using NServiceBus.IntegrationTesting.Agent;
using SampleMessages;

namespace SampleEndpoint.Testing;

public class FailingMessageScenario : Scenario
{
    public override string Name => "FailingMessage";

    public override async Task Execute(
        IMessageSession session,
        Dictionary<string, string> args,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.Parse(args["ID"]);
        await session.Send(new FailingMessage { CorrelationId = correlationId });
    }
}
