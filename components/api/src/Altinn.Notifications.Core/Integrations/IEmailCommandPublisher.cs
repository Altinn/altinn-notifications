using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines methods for publishing email notifications from the API to the Email service.
/// </summary>
public interface IEmailCommandPublisher
{
    /// <summary>
    /// Enqueues a single email notification for asynchronous delivery to the Email service.
    /// </summary>
    /// <param name="email">The email to deliver.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that completes when the publish attempt has finished. Returns <c>null</c> when the
    /// publish succeeded; otherwise returns the <see cref="Email"/> that failed to publish.
    /// </returns>
    Task<Email?> PublishAsync(Email email, CancellationToken cancellationToken);

    /// <summary>
    /// Enqueues a batch of email notifications for asynchronous delivery to the Email service.
    /// </summary>
    /// <param name="emails">The collection of emails to deliver.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that completes with a read-only list of <see cref="Email"/> objects for notifications
    /// that failed to deliver. An empty list indicates that all notifications were delivered successfully.
    /// </returns>
    Task<IReadOnlyList<Email>> PublishAsync(IReadOnlyList<Email> emails, CancellationToken cancellationToken);
}
