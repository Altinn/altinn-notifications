using System.Collections.Concurrent;

using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Core.Shared;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Sms.Integrations.LinkMobility;

/// <summary>
/// Mock implementation of <see cref="ISmsClient"/> for local development.
/// Simulates SMS sends with convention-based error scenarios:
///   - "+4741000001" → Always fails with Failed_InvalidRecipient
///   - "+4741000002" → Fails on first attempt, succeeds on retry
///   - "+4741000003" → Always fails with Failed_Rejected
///   - Any other      → Immediate success
/// </summary>
public class MockSmsClient : ISmsClient
{
    private readonly ILogger<MockSmsClient> _logger;

    private static readonly ConcurrentDictionary<Guid, int> _sendAttempts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MockSmsClient"/> class.
    /// </summary>
    public MockSmsClient(ILogger<MockSmsClient> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<Result<string, SmsClientErrorResponse>> SendAsync(Core.Sending.Sms sms)
    {
        string recipient = sms.Recipient.Trim();
        int attempt = _sendAttempts.AddOrUpdate(sms.NotificationId, 1, (_, prev) => prev + 1);

        _logger.LogInformation(
            "MockSmsClient: SendAsync for {NotificationId} to {Recipient}, attempt {Attempt}",
            sms.NotificationId,
            recipient,
            attempt);

        // Convention-based error simulation
        if (recipient == "+4741000001")
        {
            _logger.LogWarning("MockSmsClient: Simulating InvalidRecipient for {NotificationId}", sms.NotificationId);
            Result<string, SmsClientErrorResponse> error = new SmsClientErrorResponse
            {
                SendResult = Sms.Core.Status.SmsSendResult.Failed_InvalidRecipient,
                ErrorMessage = "Mock: Invalid recipient number"
            };
            return Task.FromResult(error);
        }

        if (recipient == "+4741000002" && attempt == 1)
        {
            _logger.LogWarning("MockSmsClient: Simulating transient failure for {NotificationId}", sms.NotificationId);
            Result<string, SmsClientErrorResponse> error = new SmsClientErrorResponse
            {
                SendResult = Sms.Core.Status.SmsSendResult.Failed,
                ErrorMessage = "Mock: Transient failure (will succeed on retry)"
            };
            return Task.FromResult(error);
        }

        if (recipient == "+4741000003")
        {
            _logger.LogWarning("MockSmsClient: Simulating Rejected for {NotificationId}", sms.NotificationId);
            Result<string, SmsClientErrorResponse> error = new SmsClientErrorResponse
            {
                SendResult = Sms.Core.Status.SmsSendResult.Failed_Rejected,
                ErrorMessage = "Mock: Rejected by gateway"
            };
            return Task.FromResult(error);
        }

        // Success path
        string gatewayReference = Guid.NewGuid().ToString();
        _logger.LogInformation("MockSmsClient: Simulated send for {NotificationId}, ref={GatewayReference}", sms.NotificationId, gatewayReference);
        Result<string, SmsClientErrorResponse> result = gatewayReference;
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<Result<string, SmsClientErrorResponse>> SendAsync(Core.Sending.Sms sms, int timeToLiveInSeconds)
    {
        return SendAsync(sms);
    }
}
