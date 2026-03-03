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
    public Task<Result<string, EmailClientErrorResponse>> SendEmail(Core.Sending.Email email)
    {
        string operationId = Guid.NewGuid().ToString();
        _logger.LogInformation("MockEmailServiceClient: Simulated send for {NotificationId}, operationId={OperationId}", email.NotificationId, operationId);
        Result<string, EmailClientErrorResponse> result = operationId;
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<Core.Status.EmailSendResult> GetOperationUpdate(string operationId)
    {
        _logger.LogInformation("MockEmailServiceClient: Returning Delivered for operationId={OperationId}", operationId);
        return Task.FromResult(Core.Status.EmailSendResult.Delivered);
    }
}
