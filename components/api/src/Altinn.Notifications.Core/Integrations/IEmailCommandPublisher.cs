using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Publishes email notifications from the API to the Email service.
/// </summary>
public interface IEmailCommandPublisher
{
    /// <summary>
    /// Queues a single email notification for asynchronous delivery by the Email service.
    /// </summary>
    /// <param name="email">The email to deliver.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the email has been queued.</returns>
    /// <exception cref="NotificationSizeLimitExceededException">
    /// Thrown when the email is larger than the message queue accepts. This failure is permanent and should not be retried.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled before the email is queued.
    /// </exception>
    /// <remarks>
    /// Other exceptions, such as transient message bus failures, are passed on unchanged and may be retried.
    /// </remarks>
    Task PublishAsync(Email email, CancellationToken cancellationToken);
}
