using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    /// Seeds the test correlation ID for the current async execution context.
    /// Call this from ASP.NET Core middleware (or any non-NServiceBus entry point)
    /// before sending any messages, so that <see cref="OutgoingCorrelationIdBehavior"/>
    /// can stamp the ID onto outgoing messages and the test host can correlate them.
    ///
    /// Typical use — in a web app's *.Testing Program.cs:
    ///   app.Use(async (context, next) =>
    ///   {
    ///       if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var id))
    ///           IntegrationTestingBootstrap.SetCorrelationId(id.ToString());
    ///       await next();
    ///   });
    /// </summary>
    public static void SetCorrelationId(string correlationId) =>
        AgentService.CurrentCorrelationId.Value = correlationId;

    /// <summary>
    /// Returns the test correlation ID active in the current async execution context,
    /// or <see langword="null"/> if none has been set.
    /// Use this in a <see cref="System.Net.Http.DelegatingHandler"/> registered in a
    /// *.Testing project to forward the correlation ID onto outgoing HTTP requests, so
    /// the receiving web app's middleware can call <see cref="SetCorrelationId"/> and
    /// keep the chain unbroken.
    /// </summary>
    public static string? GetCorrelationId() =>
        AgentService.CurrentCorrelationId.Value;

    /// <summary>
    /// Registers all agent pipeline behaviors on <paramref name="config"/> and returns
    /// an <see cref="AgentHandle"/> for manual lifecycle management. Use this overload
    /// when the host manages the endpoint lifecycle (e.g. ASP.NET Core with
    /// <c>UseNServiceBus</c>) and you want to wire the connection yourself via a hosted
    /// service or <c>IHostApplicationLifetime.ApplicationStarted</c>.
    /// <para>
    /// For a fully automatic setup that also registers the connection as an
    /// <c>IHostedService</c>, use the <c>IServiceCollection</c> overload instead.
    /// </para>
    /// </summary>
    public static AgentHandle Configure(
        string endpointName,
        EndpointConfiguration config,
        IEnumerable<Scenario>? scenarios = null,
        IEnumerable<TimeoutRule>? timeoutRules = null,
        IEnumerable<SkipRule>? skipRules = null)
    {
        var agentService = CreateAndConfigureAgent(endpointName, config, scenarios, timeoutRules, skipRules);
        return new AgentHandle(agentService);
    }

    /// <summary>
    /// Registers all agent pipeline behaviors on <paramref name="config"/> and adds an
    /// <c>IHostedService</c> to <paramref name="services"/> that connects the agent to
    /// the test host after the NServiceBus hosted service has started. Use this overload
    /// when hosting NServiceBus inside an ASP.NET Core web application so that the
    /// agent connection is fully managed by the generic host.
    /// </summary>
    public static void Configure(
        string endpointName,
        EndpointConfiguration config,
        IServiceCollection services,
        IEnumerable<Scenario>? scenarios = null,
        IEnumerable<TimeoutRule>? timeoutRules = null,
        IEnumerable<SkipRule>? skipRules = null)
    {
        var agentService = CreateAndConfigureAgent(endpointName, config, scenarios, timeoutRules, skipRules);
        services.AddSingleton(agentService);
        services.AddHostedService(sp =>
            new AgentConnectorHostedService(
                sp.GetRequiredService<AgentService>(), sp));
    }

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
        IEnumerable<SkipRule>? skipRules = null,
        TimeSpan sigTermGracePeriod = default)
    {
        var gracePeriod = sigTermGracePeriod > TimeSpan.Zero ? sigTermGracePeriod : TimeSpan.FromSeconds(5);

        var host = Environment.GetEnvironmentVariable("NSBUS_TESTING_HOST");
        if (string.IsNullOrEmpty(host))
            throw new InvalidOperationException(
                "NSBUS_TESTING_HOST environment variable is not set. " +
                "This project is intended to run inside an integration test only.");

        var config = configFactory();
        var agentHandle = Configure(endpointName, config, scenarios, timeoutRules, skipRules);

        var endpointInstance = await Endpoint.Start(config);

        await agentHandle.ConnectAsync(endpointInstance);

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
            await Task.Delay(System.Threading.Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException) { }

        await agentHandle.DisposeAsync();
        await endpointInstance.Stop();
    }

    static AgentService CreateAndConfigureAgent(
        string endpointName,
        EndpointConfiguration config,
        IEnumerable<Scenario>? scenarios,
        IEnumerable<TimeoutRule>? timeoutRules,
        IEnumerable<SkipRule>? skipRules)
    {
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

        var skips = skipRules?.ToList() ?? [];
        if (skips.Count > 0)
            config.Pipeline.Register(
                new SkipMessageBehavior(skips, agentService),
                "ACKs matching messages without invoking handlers and reports them to the test host");

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

        return agentService;
    }
}
