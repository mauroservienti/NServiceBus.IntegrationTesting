using Grpc.Core;
using Grpc.Net.Client;
using NServiceBus.IntegrationTesting.Proto;

namespace NServiceBus.IntegrationTesting.Agent;

/// <summary>
/// Manages the gRPC connection from the endpoint to the test host.
/// Created and wired by IntegrationTestingBootstrap.
/// </summary>
public sealed class AgentService : IAsyncDisposable
{
    /// <summary>
    /// Header name used to propagate the test correlation ID through the NServiceBus
    /// message pipeline. Set by OutgoingCorrelationIdBehavior; read by ReportingBehavior.
    /// </summary>
    internal const string CorrelationIdHeader = "NServiceBus.Testing.CorrelationId";

    /// <summary>
    /// Flows the active correlation ID down the current async execution context so that
    /// OutgoingCorrelationIdBehavior can stamp it onto messages sent within a scenario
    /// or from within a handler — without requiring user code to pass it explicitly.
    /// </summary>
    internal static readonly AsyncLocal<string?> CurrentCorrelationId = new();

    readonly string _endpointName;
    GrpcChannel? _channel;
    Grpc.Core.AsyncDuplexStreamingCall<AgentToHostMessage, HostToAgentMessage>? _call;
    IMessageSession? _session;
    IReadOnlyDictionary<string, Scenario> _scenarios = new Dictionary<string, Scenario>();

    // Becomes set once ConnectAsync has written the ConnectMessage.
    // All report methods wait on this before writing to ensure ordering.
    readonly TaskCompletionSource _connectedTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // gRPC RequestStream.WriteAsync must not be called concurrently.
    // This semaphore serializes all writes from concurrent message handlers.
    readonly SemaphoreSlim _writeLock = new(1, 1);

    internal AgentService(string endpointName)
    {
        _endpointName = endpointName;
    }

    internal void RegisterScenarios(IEnumerable<Scenario> scenarios)
    {
        _scenarios = scenarios.ToDictionary(s => s.Name);
    }

    /// <summary>
    /// Connects to the test host. Must be called after Endpoint.Start().
    /// </summary>
    public async Task ConnectAsync(IMessageSession session, CancellationToken cancellationToken = default)
    {
        _session = session;

        var host = Environment.GetEnvironmentVariable("NSBUS_TESTING_HOST")!;

        // Insecure HTTP/2 channel — fine for local test communication.
        _channel = GrpcChannel.ForAddress(host);
        var client = new TestHostService.TestHostServiceClient(_channel);
        _call = client.OpenChannel(cancellationToken: cancellationToken);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _call.RequestStream.WriteAsync(
                new AgentToHostMessage
                {
                    Connect = new ConnectMessage { EndpointName = _endpointName }
                },
                cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }

        _connectedTcs.TrySetResult();

        _ = ProcessHostMessagesAsync(cancellationToken);
    }

    async Task SendAsync(AgentToHostMessage message, CancellationToken cancellationToken)
    {
        await _connectedTcs.Task.WaitAsync(cancellationToken);
        if (_call is null) return;

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _call.RequestStream.WriteAsync(message, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    internal Task ReportHandlerInvokedAsync(
        string handlerTypeName,
        string messageTypeName,
        string? correlationId,
        SagaInfo? sagaInfo,
        CancellationToken cancellationToken = default)
    {
        var msg = new HandlerInvokedMessage
        {
            EndpointName = _endpointName,
            HandlerTypeName = handlerTypeName,
            MessageTypeName = messageTypeName,
            CorrelationId = correlationId ?? string.Empty
        };

        if (sagaInfo is not null)
        {
            msg.IsSaga = true;
            msg.SagaNotFound = sagaInfo.NotFound;
            if (!sagaInfo.NotFound)
            {
                msg.SagaTypeName = sagaInfo.TypeName;
                msg.SagaId = sagaInfo.Id;
                msg.SagaIsNew = sagaInfo.IsNew;
                msg.SagaIsCompleted = sagaInfo.IsCompleted;
            }
        }

        return SendAsync(new AgentToHostMessage { HandlerInvoked = msg }, cancellationToken);
    }

    async Task ProcessHostMessagesAsync(CancellationToken cancellationToken)
    {
        if (_call is null) return;
        try
        {
            await foreach (var message in _call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                switch (message.PayloadCase)
                {
                    case HostToAgentMessage.PayloadOneofCase.Ready:
                        // Acknowledgement from host — no action needed.
                        break;

                    case HostToAgentMessage.PayloadOneofCase.ExecuteScenario:
                        await ExecuteScenarioAsync(message.ExecuteScenario, cancellationToken);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Agent] ProcessHostMessagesAsync failed: {ex}");
        }
    }

    internal Task ReportMessageDispatchedAsync(
        string messageTypeName,
        string intent,
        string? correlationId,
        CancellationToken cancellationToken = default)
        => SendAsync(
            new AgentToHostMessage
            {
                MessageDispatched = new MessageDispatchedMessage
                {
                    EndpointName = _endpointName,
                    MessageTypeName = messageTypeName,
                    Intent = intent,
                    CorrelationId = correlationId ?? string.Empty
                }
            },
            cancellationToken);

    internal Task ReportMessageFailedAsync(
        IReadOnlyDictionary<string, string> headers,
        string exceptionMessage,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var msg = new MessageFailedMessage
        {
            EndpointName = _endpointName,
            ExceptionMessage = exceptionMessage,
            CorrelationId = correlationId ?? string.Empty
        };
        foreach (var (k, v) in headers)
            msg.Headers[k] = v;
        return SendAsync(new AgentToHostMessage { MessageFailed = msg }, cancellationToken);
    }

    async Task ExecuteScenarioAsync(ExecuteScenarioMessage cmd, CancellationToken cancellationToken)
    {
        if (_session is null) return;

        if (!_scenarios.TryGetValue(cmd.ScenarioName, out var scenario))
        {
            Console.Error.WriteLine($"[Agent] Scenario '{cmd.ScenarioName}' not found. Registered: [{string.Join(", ", _scenarios.Keys)}]");
            return;
        }

        // Set before Execute so the value flows into all async continuations,
        // including the outgoing pipeline when the scenario calls session.Send().
        CurrentCorrelationId.Value = cmd.CorrelationId;
        await scenario.Execute(_session, new Dictionary<string, string>(cmd.Args), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_call is not null)
        {
            try { await _call.RequestStream.CompleteAsync(); } catch { }
            _call.Dispose();
        }
        if (_channel is not null)
        {
            await _channel.ShutdownAsync();
            _channel.Dispose();
        }
    }
}
