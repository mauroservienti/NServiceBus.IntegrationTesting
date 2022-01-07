namespace NServiceBus.IntegrationTesting.OutOfProcess
{
    public static class Properties
    {
        const string Prefix = "NServiceBus.IntegrationTesting";
        public const string DebuggerAttached = $"{Prefix}.{nameof(DebuggerAttached)}";
    }
}
