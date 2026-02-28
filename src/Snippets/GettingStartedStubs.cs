// ReSharper disable CheckNamespace
// Stub types used only to make getting-started snippets compile.

using NServiceBus;
using NServiceBus.IntegrationTesting.Agent;

namespace YourEndpoint.Messages
{
    public class SomeCommand : ICommand
    {
        public Guid Id { get; set; }
    }
}

namespace Snippets.GettingStarted
{
    public class OrderProcessingTimeout { }

    public class OrderStatusUpdated : IMessage { }
}

namespace SampleEndpoint.Handlers
{
    public class SomeReplySagaTimeout { }
}

namespace SampleEndpoint.Testing
{
    public class FailingMessageScenario : Scenario
    {
        public override string Name => "FailingMessage";

        public override Task Execute(
            IMessageSession session,
            Dictionary<string, string> args,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
