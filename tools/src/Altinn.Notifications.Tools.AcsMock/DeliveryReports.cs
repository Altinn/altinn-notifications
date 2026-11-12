using System.Text.Json;
using System.Threading.Channels;

using Altinn.Notifications.Tools.AcsMock.Configuration;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Tools.AcsMock;

/// <summary>
/// Sends an Event Grid event to where the API reads delivery reports.
/// </summary>
internal interface IDeliveryReportSender
{
    Task SendAsync(string eventGridEvent, string? traceParent, string? traceState, CancellationToken cancellationToken);
}

/// <summary>
/// Puts the event on the Service Bus queue, as the Event Grid subscription in Azure does.
/// </summary>
internal sealed class ServiceBusDeliveryReportSender(IOptions<AcsMockSettings> options) : IDeliveryReportSender, IAsyncDisposable
{
    private readonly AcsMockSettings _settings = options.Value;
    private ServiceBusClient? _client;
    private ServiceBusSender? _sender;

    public async Task SendAsync(string eventGridEvent, string? traceParent, string? traceState, CancellationToken cancellationToken)
    {
        // Created on first use, so the mock starts without a Service Bus when delivery reports are off.
        _client ??= new ServiceBusClient(_settings.ServiceBusConnectionString);
        _sender ??= _client.CreateSender(_settings.DeliveryReportQueueName);

        var message = new ServiceBusMessage(eventGridEvent) { ContentType = "application/json" };
        if (!string.IsNullOrEmpty(traceParent))
        {
            message.ApplicationProperties["traceparent"] = traceParent;
            message.ApplicationProperties["Diagnostic-Id"] = traceParent;
        }

        if (!string.IsNullOrEmpty(traceState))
        {
            message.ApplicationProperties["tracestate"] = traceState;
        }

        await _sender.SendMessageAsync(message, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }
    }
}

/// <summary>
/// Sends a delivery report per recipient some time after an operation has been seen as succeeded,
/// like Event Grid does after ACS has handed the email over.
/// </summary>
internal sealed class DeliveryReportService(
    IDeliveryReportSender sender,
    EmailMirror mirror,
    IOptions<AcsMockSettings> options,
    TimeProvider timeProvider,
    ILogger<DeliveryReportService> logger) : BackgroundService
{
    private readonly Channel<EmailOperation> _queue = Channel.CreateUnbounded<EmailOperation>();
    private readonly AcsMockSettings _settings = options.Value;

    private bool IsEnabled => !string.IsNullOrWhiteSpace(_settings.ServiceBusConnectionString);

    /// <summary>
    /// Queues the delivery reports of an operation, once.
    /// </summary>
    public void Queue(EmailOperation operation)
    {
        if (IsEnabled && EmailScenarios.DeliveryStatus(operation.Scenario) is not null && operation.MarkReportQueued())
        {
            _queue.Writer.TryWrite(operation);
        }
    }

    /// <summary>
    /// The Event Grid event the API's <c>EmailDeliveryReportHandler</c> parses.
    /// </summary>
    internal static string CreateEvent(EmailOperation operation, string recipient, string status, DateTimeOffset now)
    {
        return JsonSerializer.Serialize(new
        {
            id = Guid.NewGuid().ToString(),
            topic = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/local/providers/Microsoft.Communication/communicationServices/acs-mock",
            subject = $"sender/{operation.Sender}/message/{operation.Id}",
            eventType = "Microsoft.Communication.EmailDeliveryReportReceived",
            dataVersion = "1.0",
            metadataVersion = "1",
            eventTime = now,
            data = new
            {
                sender = operation.Sender,
                recipient,
                messageId = operation.Id,
                status,
                deliveryStatusDetails = new
                {
                    statusMessage = status == "Delivered" ? "No error." : $"Simulated {status} by the ACS mock.",
                    recipientMailServerHostName = "acs-mock.localhost",
                },
                deliveryAttemptTimestamp = now,
                internetMessageId = $"<{operation.Id}@acs-mock.localhost>",
            },
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled)
        {
            logger.LogWarning("ACS mock sends no delivery reports, since ServiceBusConnectionString is empty");
            return;
        }

        await foreach (var operation in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            _ = SendAsync(operation, stoppingToken);
        }
    }

    private async Task SendAsync(EmailOperation operation, CancellationToken cancellationToken)
    {
        string status = EmailScenarios.DeliveryStatus(operation.Scenario)!;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_settings.DeliveryReportDelaySeconds), timeProvider, cancellationToken);

            foreach (string recipient in operation.Recipients)
            {
                string eventGridEvent = CreateEvent(operation, recipient, status, timeProvider.GetUtcNow());
                await sender.SendAsync(eventGridEvent, operation.TraceParent, operation.TraceState, cancellationToken);
                logger.LogInformation(
                    "ACS delivery report {Status} for operation {OperationId} to {Queue}\nEvent: {Event}",
                    status,
                    operation.Id,
                    _settings.DeliveryReportQueueName,
                    PayloadMasker.Mask(eventGridEvent));
            }

            mirror.Completed(operation.Id, status);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            logger.LogError(e, "ACS mock failed to send the delivery report for operation {OperationId}", operation.Id);
        }
    }
}
