namespace NServiceBus.IntegrationTesting.Agent;

/// <summary>
/// Entry-point helper for the testing-specific endpoint wrapper project.
/// Wires the agent into the endpoint configuration, starts everything,
/// and handles the full process lifecycle (Ctrl+C and SIGTERM).
///
/// Usage in SampleEndpoint.Testing/Program.cs:
///   await IntegrationTestingBootstrap.RunAsync("SampleEndpoint", SampleEndpointConfig.Create);
/// </summary>
public static class IntegrationTestingBootstrap
{
    /// <summary>
    /// Configures the agent, starts the endpoint, connects to the test host,
    /// and blocks until the process is cancelled (Ctrl+C or SIGTERM).
    /// </summary>
    /// <param name="endpointName">
    /// The logical name of the endpoint — must match what the test waits for
    /// via WaitForAgentAsync.
    /// </param>
    /// <param name="configFactory">
    /// Factory that produces a fully configured EndpointConfiguration.
    /// The factory should NOT call Endpoint.Start(); the bootstrap does that.
    /// </param>
    /// <param name="sigTermGracePeriod">
    /// How long to wait after SIGTERM (docker stop) before the CLR exits, giving the
    /// graceful stop a chance to complete. Defaults to 5 seconds. Increase for endpoints
    /// with slow saga persistence flushes or large in-flight message batches.
    /// </param>
    public static async Task RunAsync(
        string endpointName,
        Func<EndpointConfiguration> configFactory,
        IEnumerable<Scenario>? scenarios = null,
        IEnumerable<TimeoutRule>? timeoutRules = null,
        TimeSpan sigTermGracePeriod = default)
    {
        var gracePeriod = sigTermGracePeriod > TimeSpan.Zero ? sigTermGracePeriod : TimeSpan.FromSeconds(5);

        var host = Environment.GetEnvironmentVariable("NSBUS_TESTING_HOST");
        if (string.IsNullOrEmpty(host))
            throw new InvalidOperationException(
                "NSBUS_TESTING_HOST environment variable is not set. " +
                "This project is intended to run inside an integration test only.");

        var config = configFactory();

        var agentService = new AgentService(endpointName);
        agentService.RegisterScenarios(scenarios ?? []);
        config.Pipeline.Register(
            new IncomingCorrelationIdBehavior(),
            "Extracts the test correlation ID from transport headers into the async context");
        config.Pipeline.Register(
            new ReportingBehavior(agentService),
            "Reports handler invocations to the integration testing host");
        config.Pipeline.Register(
            new OutgoingCorrelationIdBehavior(),
            "Stamps the test correlation ID onto outgoing messages");
        config.Pipeline.Register(
            new OutgoingReportingBehavior(agentService),
            "Reports dispatched messages to the integration testing host");

        var rules = timeoutRules?.ToList() ?? [];
        if (rules.Count > 0)
            config.Pipeline.Register(
                new TimeoutRescheduleBehavior(rules),
                "Shortens saga timeout delays for faster test execution");

        config.Recoverability().Failed(c => c.OnMessageSentToErrorQueue((failedMessage, ct) =>
        {
            if (!failedMessage.Headers.TryGetValue(AgentService.CorrelationIdHeader, out var correlationId))
                return Task.CompletedTask;

            return agentService.ReportMessageFailedAsync(
                failedMessage.Headers,
                failedMessage.Exception.Message,
                correlationId,
                ct);
        }));

        var endpointInstance = await Endpoint.Start(config);

        await agentService.ConnectAsync(endpointInstance);

        Console.WriteLine($"{endpointName} (testing mode) started.");

        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // Handle SIGTERM (docker stop sends SIGTERM before SIGKILL).
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            cts.Cancel();
            // Give the graceful stop a moment to complete before the CLR exits.
            Task.Delay(gracePeriod).Wait();
        };

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException) { }

        await agentService.DisposeAsync();
        await endpointInstance.Stop();
    }
}
