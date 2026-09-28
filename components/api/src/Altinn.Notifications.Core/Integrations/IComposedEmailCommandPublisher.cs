using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines methods for publishing composed email notifications from the API to the Email service.
/// </summary>
public interface IComposedEmailCommandPublisher
{
    /// <summary>
    /// Enqueues a composed email notification for asynchronous delivery to the Email service.
    /// </summary>
    /// <param name="email">The composed email to deliver.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that completes with <see langword="null"/> when publishing succeeds;
    /// otherwise, the unpublished <see cref="ComposedEmail"/>.
    /// </returns>
    Task<ComposedEmail?> PublishAsync(ComposedEmail email, CancellationToken cancellationToken);

    /// <summary>
    /// Enqueues a batch of composed email notifications for asynchronous delivery to the Email service.
    /// </summary>
    /// <param name="emails">The collection of composed emails to deliver.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that completes with a read-only list of <see cref="ComposedEmail"/> objects for notifications
    /// that failed to publish. An empty list indicates that all notifications were published successfully.
    /// </returns>
    Task<IReadOnlyList<ComposedEmail>> PublishAsync(IReadOnlyList<ComposedEmail> emails, CancellationToken cancellationToken);
}
