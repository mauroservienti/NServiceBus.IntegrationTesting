using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using NServiceBus.IntegrationTesting;
using NServiceBus.IntegrationTesting.Agent;
using NUnit.Framework;

namespace Snippets.GettingStartedWebApp;

// ── Production-side static config class ─────────────────────────────────────

// begin-snippet: gs-webapp-config
public static class WebAppConfig
{
    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers();
        // ... register other services
        return builder;
    }

    public static EndpointConfiguration CreateNsbConfig(HostBuilderContext ctx)
    {
        var config = new EndpointConfiguration("WebApp");
        // ... configure transport, persistence, etc.
        return config;
    }

    public static void ConfigurePipeline(WebApplication app)
    {
        app.MapControllers();
        // ... add other middleware
    }
}
// end-snippet

// ── Correlation-ID middleware (lives in WebApp.Testing) ──────────────────────

// begin-snippet: gs-webapp-correlation-middleware
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var id))
            IntegrationTestingBootstrap.SetCorrelationId(id.ToString());

        await next(context);
    }
}
// end-snippet

// ── DelegatingHandler for outgoing HTTP (lives in WebApp.Testing or the NSB endpoint .Testing) ──

// begin-snippet: gs-webapp-propagation-handler
public class CorrelationIdPropagationHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var id = IntegrationTestingBootstrap.GetCorrelationId();
        if (id is not null)
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", id);

        return base.SendAsync(request, cancellationToken);
    }
}
// end-snippet

// ── Testing Program.cs ────────────────────────────────────────────────────────

public static class WebAppTestingProgram
{
    // begin-snippet: gs-webapp-testing-program
    public static async Task Main(string[] args)
    {
        var builder = WebAppConfig.CreateBuilder(args);

        // Wire the agent into NServiceBus and register the connector hosted service.
        // The IServiceCollection overload handles everything: behavior registration
        // and connecting to the test host after the endpoint starts.
        builder.Host.UseNServiceBus(ctx =>
        {
            var config = WebAppConfig.CreateNsbConfig(ctx);
            IntegrationTestingBootstrap.Configure("WebApp", config, builder.Services);
            return config;
        });

        // Forward the test correlation ID onto outgoing HTTP calls so the chain
        // stays connected across HTTP boundaries.
        builder.Services.AddTransient<CorrelationIdPropagationHandler>();
        builder.Services.AddHttpClient<IInventoryClient, InventoryClient>()
            .AddHttpMessageHandler<CorrelationIdPropagationHandler>();

        var app = builder.Build();

        // Seed the test correlation ID from the inbound X-Correlation-Id header
        // so the NServiceBus pipeline can stamp it on any outgoing messages.
        app.UseMiddleware<CorrelationIdMiddleware>();

        WebAppConfig.ConfigurePipeline(app);

        await app.RunAsync();
    }
    // end-snippet

    // Stubs so the snippet compiles
    public interface IInventoryClient { }
    public class InventoryClient(HttpClient http) : IInventoryClient { }
}

// ── Production Program.cs (reference only, not a snippet) ───────────────────

public static class WebAppProductionProgram
{
    // begin-snippet: gs-webapp-production-program
    public static async Task Main(string[] args)
    {
        var builder = WebAppConfig.CreateBuilder(args);
        builder.Host.UseNServiceBus(WebAppConfig.CreateNsbConfig);
        var app = builder.Build();
        WebAppConfig.ConfigurePipeline(app);
        await app.RunAsync();
    }
    // end-snippet
}

// ── Test fixture ──────────────────────────────────────────────────────────────

[TestFixture]
[NonParallelizable]
public class WhenAnOrderIsPlaced
{
    static TestEnvironment _env = null!;

    [OneTimeSetUp]
    public static async Task SetUp()
    {
        var srcDir = TestEnvironmentBuilder.FindRootByDirectory(".git", "src");

        // begin-snippet: gs-webapp-env-setup
        _env = await new TestEnvironmentBuilder()
            .WithDockerfileDirectory(srcDir)
            .UseRabbitMQ()
            .AddEndpoint("WebApp", "WebApp.Testing/Dockerfile",
                containerBuilder: b => b.WithPortBinding(8080, assignRandomHostPort: true))
            .AddEndpoint("OrdersEndpoint", "OrdersEndpoint.Testing/Dockerfile")
            .StartAsync();
        // end-snippet
    }

    [OneTimeTearDown]
    public static Task TearDown() => _env.DisposeAsync().AsTask();

    [Test]
    public async Task OrderCreated_event_should_be_dispatched()
    {
        // begin-snippet: gs-webapp-test
        var correlationId = Guid.NewGuid().ToString();

        using var http = new HttpClient
        {
            BaseAddress = new Uri(_env.GetEndpoint("WebApp").GetBaseUrl(8080))
        };
        http.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);

        await http.PostAsJsonAsync("/api/orders", new { ProductId = "SKU-42", Quantity = 1 });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var results = await _env.Observe(correlationId, cts.Token)
            .MessageDispatched("OrderCreated")
            .HandlerInvoked("OrderCreatedHandler")
            .WhenAllAsync();

        Assert.That(results.MessageDispatched("OrderCreated").EndpointName, Is.EqualTo("WebApp"));
        Assert.That(results.HandlerInvoked("OrderCreatedHandler").EndpointName, Is.EqualTo("OrdersEndpoint"));
        // end-snippet
    }
}
