using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines the contract for publishing email notifications to a message broker for sending.
/// </summary>
public interface IEmailSendPublisher
{
    /// <summary>
    /// Publishes an email notification to the configured message broker for asynchronous sending.
    /// </summary>
    /// <param name="email">The email notification to publish.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous publish operation.
    /// Returns <c>null</c> if the email was published successfully;
    /// otherwise, returns the <see cref="Guid"/> of the notification that failed to publish.
    /// </returns>
    Task<Guid?> PublishAsync(Email email, CancellationToken cancellationToken);
}
