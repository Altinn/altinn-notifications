using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Defines the contract for publishing email notifications from the API to the Email service via Azure Service Bus using Wolverine.
/// </summary>
public interface IEmailCommandPublisher
{
    /// <summary>
    /// Publishes an email notification to Azure Service Bus via Wolverine for asynchronous sending.
    /// </summary>
    /// <param name="email">The email notification to publish.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task representing the asynchronous publish operation.
    /// Returns <c>null</c> if the email was published successfully;
    /// otherwise, returns the <see cref="Guid"/> of the notification that failed to publish.
    /// </returns>
    Task<Guid?> PublishAsync(Email email, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a batch of email notifications to Azure Service Bus via Wolverine using controlled concurrency.
    /// </summary>
    /// <param name="emails">The email notifications to publish.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A read-only list of <see cref="Guid"/> values representing the notification IDs that failed to publish.
    /// An empty list indicates all notifications were published successfully.
    /// </returns>
    Task<IReadOnlyList<Guid>> PublishAsync(IReadOnlyList<Email> emails, CancellationToken cancellationToken);
}
