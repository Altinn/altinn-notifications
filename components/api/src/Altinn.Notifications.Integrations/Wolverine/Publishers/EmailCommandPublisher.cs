using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Exceptions;
using Altinn.Notifications.Shared.Publishers;

namespace Altinn.Notifications.Integrations.Wolverine.Publishers;

/// <summary>
/// Wolverine-based implementation of <see cref="IEmailCommandPublisher"/> that publishes
/// email notifications to an Azure Service Bus queue via <see cref="IWolverinePublisher"/>.
/// </summary>
public class EmailCommandPublisher(IWolverinePublisher wolverinePublisher) : IEmailCommandPublisher
{
    /// <inheritdoc/>
    public async Task PublishAsync(Email email, CancellationToken cancellationToken)
    {
        try
        {
            await wolverinePublisher.PublishCommandAsync(CreateCommand(email), cancellationToken);
        }
        catch (ContentTooLargeException ex)
        {
            throw new NotificationSizeLimitExceededException($"Email {email.NotificationId} exceeds the message queue size limit.", ex);
        }
    }

    /// <summary>
    /// Creates a <see cref="SendEmailCommand"/> from an <see cref="Email"/>.
    /// </summary>
    /// <param name="email">The email to convert.</param>
    /// <returns>The command to publish to the message bus.</returns>
    private static SendEmailCommand CreateCommand(Email email)
    {
        return new SendEmailCommand
        {
            Body = email.Body,
            Subject = email.Subject,
            ToAddress = email.ToAddress,
            FromAddress = email.FromAddress,
            NotificationId = email.NotificationId,
            ContentType = email.ContentType.ToString()
        };
    }
}
