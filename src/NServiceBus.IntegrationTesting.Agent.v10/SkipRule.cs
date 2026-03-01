namespace NServiceBus.IntegrationTesting.Agent;

/// <summary>
/// Defines a rule that causes matching incoming messages to be silently ACKed
/// without invoking any handlers. The skip is reported to the test host as a
/// <c>MessageSkippedEvent</c> so tests can observe it via ObserveContext.MessageSkipped().
///
/// Register via IntegrationTestingBootstrap.RunAsync(..., skipRules: [...]).
///
/// Typical use: testing saga timeout / escalation paths where a downstream endpoint
/// must not reply, simulating an unavailable service.
/// </summary>
public abstract class SkipRule
{
    /// <summary>
    /// Creates a rule that skips all messages of type <typeparamref name="T"/>.
    /// </summary>
    public static SkipRule For<T>() => new SkipRule<T>(null);

    /// <summary>
    /// Creates a rule that skips messages of type <typeparamref name="T"/> only when
    /// <paramref name="predicate"/> returns true for the message instance.
    /// The predicate receives the deserialized message so callers can inspect its content.
    /// </summary>
    public static SkipRule For<T>(Func<T, bool> predicate) => new SkipRule<T>(predicate);

    internal abstract bool ShouldSkip(Type messageType, object message);
}

sealed class SkipRule<T>(Func<T, bool>? predicate) : SkipRule
{
    internal override bool ShouldSkip(Type messageType, object message)
    {
        if (messageType != typeof(T) && !messageType.IsAssignableTo(typeof(T)))
            return false;

        return predicate is null || predicate((T)message);
    }
}
