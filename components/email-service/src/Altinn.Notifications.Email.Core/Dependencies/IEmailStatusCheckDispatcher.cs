namespace Altinn.Notifications.Email.Core.Dependencies;

/// <summary>
/// Provides functionality for initiating a status check after an email send request has been successfully accepted by Azure Communication Services (ACS).
/// </summary>
public interface IEmailStatusCheckDispatcher
{
    /// <summary>
    /// Triggers a status-check operation for a specific ACS email send request.
    /// </summary>
    /// <param name="notificationId">
    /// The unique identifier of the notification associated with the email send request.
    /// </param>
    /// <param name="operationId">
    /// The operation identifier returned by ACS upon successful submission of the email.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous status-check dispatch process.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="operationId"/> is <see langword="null"/>, empty, or consists only of white-space characters.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="notificationId"/> is <see cref="Guid.Empty"/>.
    /// </exception>
    Task DispatchAsync(Guid notificationId, string operationId);
}
