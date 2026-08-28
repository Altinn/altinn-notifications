using System.Text.Json.Serialization;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Sending;

using Microsoft.Extensions.Logging;

using Wolverine;

namespace Altinn.Notifications.Email.Integrations.Clients;

/// <summary>
/// Mock implementation of <see cref="IEmailServiceClient"/> for local development.
/// Simulates successful email sends without requiring Azure Communication Services credentials.
/// </summary>
public class MockEmailServiceClient : IEmailServiceClient
{
    private const int AcsSendExecutionTimeMs = 70;
    private const int AcsUpdateExecutionTimeMs = 40;
    private const int AcsDeliveryReportDelayTimeMs = 2000;

    private readonly ILogger<MockEmailServiceClient> _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockEmailServiceClient"/> class.
    /// </summary>
    public MockEmailServiceClient(IServiceProvider serviceProvider, ILogger<MockEmailServiceClient> logger)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task<Result<string, EmailClientErrorResponse>> SendEmail(Core.Sending.Email email)
    {
        string operationId = email.NotificationId.ToString();
        _logger.LogError("MockEmailServiceClient: Simulated email send for {NotificationId}, operationId={OperationId}", email.NotificationId, operationId);
        Result<string, EmailClientErrorResponse> result = operationId;
        await Task.Delay(AcsSendExecutionTimeMs);
        return result;
    }

    /// <inheritdoc/>
    public async Task<Result<ComposedEmailSendResult, EmailClientErrorResponse>> SendComposedEmail(ComposedEmail email, CancellationToken cancellationToken = default)
    {
        string operationId = Guid.NewGuid().ToString() + "_" + email.NotificationId.ToString();
        ////_logger.LogError("MockEmailServiceClient: Simulated composed email send for {NotificationId}, operationId={OperationId}", email.NotificationId, operationId);
        await Task.Delay(AcsSendExecutionTimeMs);

        return new ComposedEmailSendResult
        {
            OperationId = operationId,
            TotalAttachmentSizeBytes = 0
        };
    }

    /// <inheritdoc/>
    public async Task<Core.Status.EmailSendResult> GetOperationUpdate(string operationId)
    {
        ////_logger.LogError("MockEmailServiceClient: Returning Delivered for operationId={OperationId}", operationId);
        await Task.Delay(AcsUpdateExecutionTimeMs);

        _ = Task.Run(async () =>
        {
            await Task.Delay(AcsDeliveryReportDelayTimeMs);
            await new DeliveryReportPublisher(_serviceProvider).DispatchAsync(operationId);
        });

        return Core.Status.EmailSendResult.Delivered;
    }
}

/// <summary>
/// Mock of ACS delivery report publisher
/// </summary>
public class DeliveryReportPublisher : Altinn.Notifications.Shared.Publishers.WolverinePublisher
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryReportPublisher"/> class.
    /// </summary>
    /// <param name="serviceProvider">
    /// The service provider used to resolve a scoped <see cref="IMessageBus"/> instance for each dispatch.
    /// </param>
    public DeliveryReportPublisher(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    /// <summary>
    /// Dispatches a mock ACS delivery report event to the message bus.
    /// </summary>
    /// <returns></returns>
    public async Task DispatchAsync(string operationId)
    {
        var command = new MockEmailDeliveryReportCommand
        {
            Data = new EmailDeliveryData
            {
                Sender = "noreply@altinn.cloud",
                Recipient = "nullstilt@altinn.xyz",
                InternetMessageId = "long string",
                MessageId = Guid.Parse(operationId),
                Status = "Delivered",
                DeliveryStatusDetails = new DeliveryStatusDetails
                {
                    StatusMessage = string.Empty,
                    RecipientMailServerHostName = "route2.mx.cloudflare.net"
                },
                DeliveryAttemptTimestamp = DateTimeOffset.UtcNow
            },
            EventTime = DateTimeOffset.UtcNow,
            EventType = "Microsoft.Communication.EmailDeliveryReportReceived",
            Id = Guid.NewGuid(),
            MetadataVersion = "1",
            Subject = "Mock delivery report",
            Topic = "mock/email/deliveryreport",
            DataVersion = "1.0",
        };

        await PublishCommandAsync(command);
    }
}

/// <summary>
/// Represents a mock email delivery report command for testing purposes.
/// </summary>
public class MockEmailDeliveryReportCommand
{
    /// <summary>
    /// Gets or sets the unique identifier for the event.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the topic of the event.
    /// </summary>
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subject of the event.
    /// </summary>
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email delivery data.
    /// </summary>
    [JsonPropertyName("data")]
    public EmailDeliveryData Data { get; set; } = new();

    /// <summary>
    /// Gets or sets the type of the event.
    /// </summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version of the data schema.
    /// </summary>
    [JsonPropertyName("dataVersion")]
    public string DataVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version of the metadata schema.
    /// </summary>
    [JsonPropertyName("metadataVersion")]
    public string MetadataVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time the event occurred.
    /// </summary>
    [JsonPropertyName("eventTime")]
    public DateTimeOffset EventTime { get; set; }
}

/// <summary>
/// Represents the data payload of an email delivery event.
/// </summary>
public class EmailDeliveryData
{
    /// <summary>
    /// Gets or sets the sender email address.
    /// </summary>
    [JsonPropertyName("sender")]
    public string Sender { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recipient email address.
    /// </summary>
    [JsonPropertyName("recipient")]
    public string Recipient { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Internet Message ID of the email.
    /// </summary>
    [JsonPropertyName("internetMessageId")]
    public string InternetMessageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique identifier for the message.
    /// </summary>
    [JsonPropertyName("messageId")]
    public Guid MessageId { get; set; }

    /// <summary>
    /// Gets or sets the delivery status of the email.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the detailed delivery status information.
    /// </summary>
    [JsonPropertyName("deliveryStatusDetails")]
    public DeliveryStatusDetails DeliveryStatusDetails { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp of the delivery attempt.
    /// </summary>
    [JsonPropertyName("deliveryAttemptTimestamp")]
    public DateTimeOffset DeliveryAttemptTimestamp { get; set; }
}

/// <summary>
/// Represents detailed delivery status information for an email.
/// </summary>
public class DeliveryStatusDetails
{
    /// <summary>
    /// Gets or sets the status message describing the delivery result.
    /// </summary>
    [JsonPropertyName("statusMessage")]
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hostname of the recipient's mail server.
    /// </summary>
    [JsonPropertyName("recipientMailServerHostName")]
    public string RecipientMailServerHostName { get; set; } = string.Empty;
}
