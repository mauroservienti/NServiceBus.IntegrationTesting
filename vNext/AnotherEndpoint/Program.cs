using AnotherEndpoint;

Console.Title = "AnotherEndpoint";

var endpointInstance = await Endpoint.Start(AnotherEndpointConfig.Create());

Console.WriteLine("AnotherEndpoint started. Press Ctrl+C to stop.");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try { await Task.Delay(Timeout.Infinite, cts.Token); }
catch (OperationCanceledException) { }

await endpointInstance.Stop();
