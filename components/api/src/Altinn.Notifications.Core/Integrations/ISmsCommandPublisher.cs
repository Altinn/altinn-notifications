using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Publishes SMS notifications from the API to the SMS service.
/// </summary>
public interface ISmsCommandPublisher
{
    /// <summary>
    /// Queues a single SMS notification for asynchronous delivery by the SMS service.
    /// </summary>
    /// <param name="sms">The SMS to deliver, including the message body, sender and recipient.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the SMS has been queued.</returns>
    /// <exception cref="NotificationSizeLimitExceededException">
    /// Thrown when the SMS is larger than the message queue accepts. This failure is permanent and should not be retried.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled before the SMS is queued.
    /// </exception>
    /// <remarks>
    /// Other exceptions, such as transient message bus failures, are passed on unchanged and may be retried.
    /// </remarks>
    Task PublishAsync(Sms sms, CancellationToken cancellationToken);
}
