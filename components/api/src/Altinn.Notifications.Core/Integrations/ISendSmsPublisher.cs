using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines the contract for publishing SMS notifications from the API to the SMS service.
/// Implementations include Kafka-based publishing and Azure Service Bus publishing via Wolverine.
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
    /// A task that represents the asynchronous operation. Returns <c>null</c> if the SMS was successfully published,
    /// or the <see cref="Sms.NotificationId"/> if the publish operation failed.
    /// </returns>
    Task<Sms?> PublishAsync(Sms sms, CancellationToken cancellationToken);

    /// <summary>
    /// Enqueues a batch of SMS notifications for asynchronous delivery to the SMS service.
    /// </summary>
    /// <param name="smsList">The collection of SMS notifications to deliver.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that completes with a read-only list of <see cref="Sms"/> objects for notifications
    /// that failed to deliver. An empty list indicates that all notifications were delivered successfully.
    /// </returns>
    Task<IReadOnlyList<Sms>> PublishAsync(IReadOnlyList<Sms> smsList, CancellationToken cancellationToken);
}
