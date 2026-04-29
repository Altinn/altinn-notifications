using Altinn.Notifications.Email.Core.Status;

namespace Altinn.Notifications.Email.Core.Dependencies;

/// <summary>
/// Provides functionality for dispatching the transient result of an email send operation
/// to the Notifications API so the notification status can be updated.
/// </summary>
public interface IEmailSendResultDispatcher
{
    /// <summary>
    /// Dispatches a transient <see cref="SendOperationResult"/> to the Notifications API.
    /// </summary>
    /// <param name="result">
    /// The transient result of the email send operation, containing the notification identifier,
    /// the Azure Communication Services operation identifier, and the send result.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous dispatch operation.</returns>
    Task DispatchAsync(SendOperationResult result);
}
