using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// A no-operation implementation of <see cref="IEmailCommandPublisherFactory"/> that serves as a placeholder.
/// This factory is used when Wolverine is disabled or not configured.
/// It prevents dependency injection errors but doesn't actually publish emails.
/// </summary>
internal sealed class NoOpEmailCommandPublisherFactory : IEmailCommandPublisherFactory
{
    /// <inheritdoc/>
    public IEmailCommandPublisher CreatePublisher()
    {
        return new NoOpEmailCommandPublisher();
    }

    /// <inheritdoc/>
    public Task<Guid?> PublishAsync(Email email, CancellationToken cancellationToken)
    {
        // No-op implementation - returns the notification ID to indicate "failure" 
        // so the email will remain in "new" status for retry via other mechanisms
        return Task.FromResult<Guid?>(email.NotificationId);
    }

    /// <summary>
    /// A no-operation implementation of <see cref="IEmailCommandPublisher"/>.
    /// </summary>
    private sealed class NoOpEmailCommandPublisher : IEmailCommandPublisher
    {
        /// <inheritdoc/>
        public Task<Guid?> PublishAsync(Email email, CancellationToken cancellationToken)
        {
            // Return the notification ID to indicate "failure" for retry
            return Task.FromResult<Guid?>(email.NotificationId);
        }
    }
}
