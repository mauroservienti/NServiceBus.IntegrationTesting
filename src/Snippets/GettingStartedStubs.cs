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
    public class OrderProcessingTimeout
    {
        public TimeSpan CustomDelay { get; init; }
    }

    public class OrderStatusUpdated : IMessage { }
}

namespace SampleEndpoint.Handlers
{
    public class SomeReplySagaTimeout { }
}

namespace Snippets.GettingStartedSkip
{
    public class ProcessPayment : ICommand
    {
        public decimal Amount { get; set; }
    }
}

namespace Snippets.GettingStartedWebApp
{
    // Stub handler name referenced in test fixture assertion
    public class OrderCreatedHandler { }

    // Stub message type dispatched by the web app
    public class OrderCreated : NServiceBus.IEvent { }
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
