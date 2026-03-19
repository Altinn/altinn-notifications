using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// A no-operation implementation of <see cref="IEmailSendPublisherFactory"/> that serves as a placeholder.
/// This factory is used when Wolverine is disabled or not configured.
/// It prevents dependency injection errors but doesn't actually publish emails.
/// </summary>
internal sealed class NoOpEmailSendPublisherFactory : IEmailSendPublisherFactory
{
    /// <inheritdoc/>
    public IEmailSendPublisher CreatePublisher()
    {
        return new NoOpEmailSendPublisher();
    }

    /// <inheritdoc/>
    public Task<Guid?> PublishAsync(Email email, CancellationToken cancellationToken)
    {
        // No-op implementation - returns the notification ID to indicate "failure" 
        // so the email will remain in "new" status for retry via other mechanisms
        return Task.FromResult<Guid?>(email.NotificationId);
    }

    /// <summary>
    /// A no-operation implementation of <see cref="IEmailSendPublisher"/>.
    /// </summary>
    private sealed class NoOpEmailSendPublisher : IEmailSendPublisher
    {
        /// <inheritdoc/>
        public Task<Guid?> PublishAsync(Email email, CancellationToken cancellationToken)
        {
            // Return the notification ID to indicate "failure" for retry
            return Task.FromResult<Guid?>(email.NotificationId);
        }
    }
}
