using SampleMessages;

namespace SampleEndpoint.Handlers;

class SomeMessageHandler : IHandleMessages<SomeMessage>
{
    static readonly HttpClient _http = new();

    public async Task Handle(SomeMessage message, IMessageHandlerContext context)
    {
        Console.WriteLine($"[SampleEndpoint] Handling SomeMessage {message.Id}");

        // Calls an external HTTP service whose URL is read from an environment variable
        // (12-factor pattern). In the *.Testing build the variable is set to the embedded
        // WireMock server address so tests can stub and verify the interaction.
        var externalServiceUrl = Environment.GetEnvironmentVariable("WIREMOCK_URL");
        if (externalServiceUrl is not null)
        {
            var greeting = await _http.GetStringAsync($"{externalServiceUrl}/api/greeting", context.CancellationToken);
            Console.WriteLine($"[SampleEndpoint] External service responded: {greeting}");
        }

        await context.Send(new AnotherMessage { CorrelationId = message.Id });
    }
}
