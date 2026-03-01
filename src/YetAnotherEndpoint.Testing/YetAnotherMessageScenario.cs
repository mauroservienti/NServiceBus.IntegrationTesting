using NServiceBus;
using NServiceBus.IntegrationTesting.Agent;
using SampleMessages;

namespace YetAnotherEndpoint.Testing;

public class YetAnotherMessageScenario : Scenario
{
    public override string Name => "YetAnotherMessage";

    public override async Task Execute(
        IMessageSession session,
        Dictionary<string, string> args,
        CancellationToken cancellationToken = default)
        => await session.SendLocal(new YetAnotherMessage { CorrelationId = Guid.Parse(args["ID"]) });
}
