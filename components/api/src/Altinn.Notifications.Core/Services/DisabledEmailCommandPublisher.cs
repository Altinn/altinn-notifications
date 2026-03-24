using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// A no-op implementation of <see cref="IEmailCommandPublisher"/> registered as the default when Wolverine is not configured.
/// Throws <see cref="InvalidOperationException"/> if ever called, which indicates a misconfiguration:
/// <c>EnableSendEmailPublisher</c> is <c>true</c> but <c>EnableWolverine</c> is <c>false</c>,
/// so no real publisher was registered.
/// </summary>
internal sealed class DisabledEmailCommandPublisher : IEmailCommandPublisher
{
    /// <summary>
    /// Always throws <see cref="InvalidOperationException"/>; this is a no-op placeholder that must never be called.
    /// </summary>
    /// <param name="email">The email that would have been published.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="InvalidOperationException">
    /// Always thrown. <c>EnableSendEmailPublisher</c> is <c>true</c> but Wolverine is not configured.
    /// Set <c>WolverineSettings:EnableWolverine</c> to <c>true</c> or set <c>NotificationConfig:EnableSendEmailPublisher</c> to <c>false</c> to use Kafka.
    /// </exception>
    public Task<Email?> PublishAsync(Email email, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(
            $"{nameof(DisabledEmailCommandPublisher)} was called for notification {email.NotificationId}, " +
            "which means 'EnableSendEmailPublisher' is true but Wolverine is not configured. " +
            "Either set 'WolverineSettings:EnableWolverine' to true, or set 'NotificationConfig:EnableSendEmailPublisher' to false to use Kafka.");
    }

    /// <summary>
    /// Always throws <see cref="InvalidOperationException"/>; this is a no-op placeholder that must never be called.
    /// </summary>
    /// <param name="emails">The collection of emails that would have been published.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="InvalidOperationException">
    /// Always thrown. <c>EnableSendEmailPublisher</c> is <c>true</c> but Wolverine is not configured.
    /// Set <c>WolverineSettings:EnableWolverine</c> to <c>true</c> or set <c>NotificationConfig:EnableSendEmailPublisher</c> to <c>false</c> to use Kafka.
    /// </exception>
    public Task<IReadOnlyList<Email>> PublishAsync(IReadOnlyList<Email> emails, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(
            $"{nameof(DisabledEmailCommandPublisher)} was called for a batch of {emails.Count} notification(s), " +
            "which means 'EnableSendEmailPublisher' is true but Wolverine is not configured. " +
            "Either set 'WolverineSettings:EnableWolverine' to true, or set 'NotificationConfig:EnableSendEmailPublisher' to false to use Kafka.");
    }
}
