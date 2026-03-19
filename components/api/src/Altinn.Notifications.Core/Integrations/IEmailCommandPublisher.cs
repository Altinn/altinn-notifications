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
}
