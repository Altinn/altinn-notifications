namespace Altinn.Notifications.Email.Core.Dependencies;

/// <summary>
/// Provides functionality for initiating a status check after an email send request has been successfully accepted by Azure Communication Services (ACS).
/// </summary>
public interface IEmailStatusCheckDispatcher
{
    /// <summary>
    /// Triggers a status-check operation for a specific ACS email send request.
    /// </summary>
    /// <param name="notificationId">The unique identifier of the notification.</param>
    /// <param name="operationId">The operation identifier returned by ACS upon successful submission.</param>
    /// <param name="encodedAttachmentsSize">Total base64-encoded attachment size in bytes; 0 for standard emails.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous dispatch process.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="operationId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="notificationId"/> is <see cref="Guid.Empty"/>.</exception>
    Task DispatchAsync(Guid notificationId, string operationId, long encodedAttachmentsSize = 0);
}
