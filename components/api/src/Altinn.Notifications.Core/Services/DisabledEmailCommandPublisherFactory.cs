using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// A disabled implementation of <see cref="IEmailCommandPublisherFactory"/> used when Wolverine is not configured.
/// Returns the notification ID on every call to signal that publishing did not occur,
/// keeping the email in "new" status so it can be retried via other mechanisms.
/// </summary>
internal sealed class DisabledEmailCommandPublisherFactory : IEmailCommandPublisherFactory
{
    /// <inheritdoc/>
    public IEmailCommandPublisher CreatePublisher()
    {
        return new DisabledEmailCommandPublisher();
    }

    /// <inheritdoc/>
    public Task<Guid?> PublishAsync(Email email, CancellationToken cancellationToken)
    {
        // No-op implementation - returns the notification ID to indicate "failure" 
        // so the email will remain in "new" status for retry via other mechanisms
        return Task.FromResult<Guid?>(email.NotificationId);
    }

    /// <summary>
    /// A disabled implementation of <see cref="IEmailCommandPublisher"/> used when Wolverine is not configured.
    /// </summary>
    private sealed class DisabledEmailCommandPublisher : IEmailCommandPublisher
    {
        /// <inheritdoc/>
        public Task<Guid?> PublishAsync(Email email, CancellationToken cancellationToken)
        {
            // Return the notification ID to indicate "failure" for retry
            return Task.FromResult<Guid?>(email.NotificationId);
        }
    }
}
