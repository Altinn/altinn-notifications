using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Email.Core.Sending;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Email.Integrations.Clients;

/// <summary>
/// Mock implementation of <see cref="IEmailServiceClient"/> for local development.
/// Simulates successful email sends without requiring Azure Communication Services credentials.
/// </summary>
public class MockEmailServiceClient : IEmailServiceClient
{
    private readonly ILogger<MockEmailServiceClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockEmailServiceClient"/> class.
    /// </summary>
    public MockEmailServiceClient(ILogger<MockEmailServiceClient> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<string, EmailClientErrorResponse>> SendEmail(Core.Sending.Email email)
    {
        string operationId = Guid.NewGuid().ToString() + "_" + email.NotificationId.ToString();
        _logger.LogError("MockEmailServiceClient: Simulated email send for {NotificationId}, operationId={OperationId}", email.NotificationId, operationId);
        Result<string, EmailClientErrorResponse> result = operationId;
        await Task.Delay(70);
        return result;
    }

    /// <inheritdoc/>
    public async Task<Result<ComposedEmailSendResult, EmailClientErrorResponse>> SendComposedEmail(ComposedEmail email, CancellationToken cancellationToken = default)
    {
        string operationId = Guid.NewGuid().ToString() + "_" + email.NotificationId.ToString();
        _logger.LogError("MockEmailServiceClient: Simulated composed email send for {NotificationId}, operationId={OperationId}", email.NotificationId, operationId);
        await Task.Delay(70);

        return new ComposedEmailSendResult
        {
            OperationId = operationId,
            TotalAttachmentSizeBytes = 0
        };
    }

    /// <inheritdoc/>
    public async Task<Core.Status.EmailSendResult> GetOperationUpdate(string operationId)
    {
        _logger.LogError("MockEmailServiceClient: Returning Delivered for operationId={OperationId}", operationId);
        await Task.Delay(40);
        return Core.Status.EmailSendResult.Delivered;
    }
}
