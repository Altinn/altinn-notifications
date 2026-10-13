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
    /// A task that completes when the publish attempt has finished.
    /// </returns>
    Task PublishAsync(Email email, CancellationToken cancellationToken);
}
