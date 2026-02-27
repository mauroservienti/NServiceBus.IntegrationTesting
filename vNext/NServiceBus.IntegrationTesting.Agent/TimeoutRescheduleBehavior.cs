using NServiceBus.DelayedDelivery;
using NServiceBus.Pipeline;
using NServiceBus.Transport;

namespace NServiceBus.IntegrationTesting.Agent;

/// <summary>
/// Intercepts outgoing sends and shortens the delivery delay for any timeout message
/// type that has a registered <see cref="TimeoutRule"/>.  This allows *.Testing
/// projects to keep saga timeout values realistic in production while still running
/// integration tests in seconds rather than minutes.
/// </summary>
sealed class TimeoutRescheduleBehavior(IReadOnlyList<TimeoutRule> rules)
    : Behavior<IOutgoingSendContext>
{
    public override Task Invoke(IOutgoingSendContext context, Func<Task> next)
    {
        foreach (var rule in rules)
        {
            if (!rule.TryGetDelay(context.Message.MessageType, context.Message.Instance, out var delay))
                continue;

            if (context.Extensions.TryGet<DispatchProperties>(out var constraints)
                && (constraints.DoNotDeliverBefore is not null || constraints.DelayDeliveryWith is not null))
            {
                constraints.DoNotDeliverBefore = new DoNotDeliverBefore(DateTime.UtcNow.Add(delay));
                constraints.DelayDeliveryWith = null;
            }

            break;
        }

        return next();
    }
}
