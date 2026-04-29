using Altinn.Notifications.Sms.Core.Status;

namespace Altinn.Notifications.Sms.Core.Dependencies;

/// <summary>
/// Provides functionality for dispatching the transient result of an SMS send operation
/// to the Notifications API so the notification status can be updated.
/// </summary>
public interface ISmsSendResultDispatcher
{
    /// <summary>
    /// Dispatches a transient <see cref="SendOperationResult"/> to the Notifications API.
    /// </summary>
    /// <param name="result">
    /// The transient result of the SMS send operation, containing the notification identifier,
    /// the SMS gateway reference, and the send result.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous dispatch operation.</returns>
    Task DispatchAsync(SendOperationResult result);
}
