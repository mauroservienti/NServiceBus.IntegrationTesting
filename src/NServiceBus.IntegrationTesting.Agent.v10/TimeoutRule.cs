namespace NServiceBus.IntegrationTesting.Agent;

/// <summary>
/// Defines a rule that shortens a saga timeout for faster test execution.
/// The rule is registered at endpoint startup (in the *.Testing project) and applies
/// to every timeout request for the matching message type during the test run.
///
/// Register via IntegrationTestingBootstrap.RunAsync(..., timeoutRules: [...]).
/// </summary>
public abstract class TimeoutRule
{
    /// <summary>
    /// Creates a rule that replaces the scheduled delivery for <typeparamref name="T"/>
    /// with a fixed delay from the moment the timeout is requested.
    /// </summary>
    public static TimeoutRule For<T>(TimeSpan delay) => new TimeoutRule<T>(_ => delay);

    /// <summary>
    /// Creates a rule that computes the new delay from the timeout message instance.
    /// </summary>
    public static TimeoutRule For<T>(Func<T, TimeSpan> rule) => new TimeoutRule<T>(rule);

    internal abstract bool TryGetDelay(Type messageType, object message, out TimeSpan delay);
}

sealed class TimeoutRule<T>(Func<T, TimeSpan> rule) : TimeoutRule
{
    internal override bool TryGetDelay(Type messageType, object message, out TimeSpan delay)
    {
        if (messageType != typeof(T))
        {
            delay = default;
            return false;
        }

        delay = rule((T)message);
        return true;
    }
}
