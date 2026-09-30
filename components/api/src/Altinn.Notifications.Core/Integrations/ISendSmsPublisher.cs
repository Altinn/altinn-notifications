using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines the contract for publishing SMS notifications from the API to the SMS service.
/// </summary>
public interface ISendSmsPublisher
{
    /// <summary>
    /// Publishes an SMS notification asynchronously.
    /// </summary>
    /// <remarks>
    /// This method attempts to publish the SMS notification to the message bus. 
    /// If the operation is canceled, the task will complete with a cancellation exception.
    /// </remarks>
    /// <param name="sms">The SMS object containing the message body and recipient information.</param>
    /// <param name="cancellationToken">The cancellation token used to propagate notification that the operation should be canceled.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. Returns <c>null</c> when publish succeeds.
    /// </returns>
    Task<Sms?> PublishAsync(Sms sms, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a batch of SMS notifications asynchronously.
    /// </summary>
    /// <param name="smsList">The SMS notifications to publish.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// A task that completes with the subset of SMS notifications that failed to publish.
    /// </returns>
    Task<IReadOnlyList<Sms>> PublishAsync(IReadOnlyList<Sms> smsList, CancellationToken cancellationToken);
}
