using System.Collections.Concurrent;

using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Sending;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Email.Integrations.Clients;

/// <summary>
/// Mock implementation of <see cref="IEmailServiceClient"/> for local development.
/// Simulates email sends with convention-based error scenarios:
///   - "transient-fail@test.com"  → Fails with TransientError on first attempt, succeeds on retry
///   - "permanent-fail@test.com"  → Always fails with Failed
///   - "bounce@test.com"          → Always fails with Failed_Bounced
///   - "slow-delivery@test.com"   → Send succeeds, but GetOperationUpdate returns Sending twice then Delivered
///   - Any other address           → Immediate success + Delivered
/// </summary>
public class MockEmailServiceClient : IEmailServiceClient
{
    private readonly ILogger<MockEmailServiceClient> _logger;

    private static readonly ConcurrentDictionary<Guid, int> _sendAttempts = new();
    private static readonly ConcurrentDictionary<string, int> _statusPollCounts = new();
    private static readonly ConcurrentDictionary<string, string> _operationToAddress = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MockEmailServiceClient"/> class.
    /// </summary>
    public MockEmailServiceClient(ILogger<MockEmailServiceClient> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<Result<string, EmailClientErrorResponse>> SendEmail(Core.Sending.Email email)
    {
        string toAddress = email.ToAddress.ToLowerInvariant().Trim();
        int attempt = _sendAttempts.AddOrUpdate(email.NotificationId, 1, (_, prev) => prev + 1);

        _logger.LogInformation(
            "MockEmailServiceClient: SendEmail for {NotificationId} to {ToAddress}, attempt {Attempt}",
            email.NotificationId,
            toAddress,
            attempt);

        // Convention-based error simulation
        if (toAddress == "transient-fail@test.com" && attempt == 1)
        {
            _logger.LogWarning("MockEmailServiceClient: Simulating TransientError for {NotificationId}", email.NotificationId);
            Result<string, EmailClientErrorResponse> error = new EmailClientErrorResponse
            {
                SendResult = Core.Status.EmailSendResult.Failed_TransientError,
                IntermittentErrorDelay = 5
            };
            return Task.FromResult(error);
        }

        if (toAddress == "permanent-fail@test.com")
        {
            _logger.LogWarning("MockEmailServiceClient: Simulating permanent failure for {NotificationId}", email.NotificationId);
            Result<string, EmailClientErrorResponse> error = new EmailClientErrorResponse
            {
                SendResult = Core.Status.EmailSendResult.Failed
            };
            return Task.FromResult(error);
        }

        if (toAddress == "bounce@test.com")
        {
            _logger.LogWarning("MockEmailServiceClient: Simulating bounce for {NotificationId}", email.NotificationId);
            Result<string, EmailClientErrorResponse> error = new EmailClientErrorResponse
            {
                SendResult = Core.Status.EmailSendResult.Failed_Bounced
            };
            return Task.FromResult(error);
        }

        // Success path
        string operationId = Guid.NewGuid().ToString();
        _operationToAddress[operationId] = toAddress;
        _logger.LogInformation("MockEmailServiceClient: Simulated send for {NotificationId}, operationId={OperationId}", email.NotificationId, operationId);
        Result<string, EmailClientErrorResponse> result = operationId;
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<Core.Status.EmailSendResult> GetOperationUpdate(string operationId)
    {
        int pollCount = _statusPollCounts.AddOrUpdate(operationId, 1, (_, prev) => prev + 1);
        string address = _operationToAddress.GetValueOrDefault(operationId, string.Empty);

        // Slow delivery: returns Sending for first 2 polls, then Delivered
        if (address == "slow-delivery@test.com" && pollCount <= 2)
        {
            _logger.LogInformation(
                "MockEmailServiceClient: Returning Sending for operationId={OperationId} (poll {PollCount}/3)",
                operationId,
                pollCount);
            return Task.FromResult(Core.Status.EmailSendResult.Sending);
        }

        _logger.LogInformation("MockEmailServiceClient: Returning Delivered for operationId={OperationId}", operationId);
        return Task.FromResult(Core.Status.EmailSendResult.Delivered);
    }
}
