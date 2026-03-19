using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// A disabled implementation of <see cref="IEmailCommandPublisher"/> used when Wolverine is not configured.
/// Returns the notification ID on every call to signal that publishing did not occur,
/// keeping the email in "new" status so it can be retried via other mechanisms.
/// </summary>
internal sealed class DisabledEmailCommandPublisher : IEmailCommandPublisher
{
    /// <inheritdoc/>
    public Task<Guid?> PublishAsync(Email email, CancellationToken cancellationToken)
    {
        return Task.FromResult<Guid?>(email.NotificationId);
    }
}
