using NServiceBus;
using SampleEndpoint;

Console.Title = "SampleEndpoint";

var endpointInstance = await Endpoint.Start(SampleEndpointConfig.Create());

Console.WriteLine("SampleEndpoint started. Press Ctrl+C to stop.");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException) { }

await endpointInstance.Stop();
