using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Integrations;

/// <summary>
/// Factory interface for creating <see cref="IEmailSendPublisher"/> instances.
/// This factory allows singleton services to safely access scoped email publishing functionality.
/// </summary>
public interface IEmailSendPublisherFactory
{
    /// <summary>
    /// Creates an <see cref="IEmailSendPublisher"/> instance within the current service scope.
    /// </summary>
    /// <returns>An instance of <see cref="IEmailSendPublisher"/>.</returns>
    IEmailSendPublisher CreatePublisher();

    /// <summary>
    /// Publishes an email notification by creating a publisher within the appropriate service scope.
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
