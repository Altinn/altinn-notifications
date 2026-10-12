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
    /// A task that completes when publishing succeeds.
    /// </returns>
    /// <exception cref="Exception">Thrown when publishing fails.</exception>
    Task PublishAsync(ComposedEmail email, CancellationToken cancellationToken);
}
