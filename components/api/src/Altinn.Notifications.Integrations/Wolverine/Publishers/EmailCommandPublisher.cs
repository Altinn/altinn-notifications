using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Publishers;

namespace Altinn.Notifications.Integrations.Wolverine.Publishers;

/// <summary>
/// Wolverine-based implementation of <see cref="IEmailCommandPublisher"/> that publishes
/// email notifications to an Azure Service Bus queue via <see cref="IMessageBusPublisher"/>.
/// </summary>
public class EmailCommandPublisher(IMessageBusPublisher messageBusPublisher) : IEmailCommandPublisher
{
    private readonly IMessageBusPublisher _messageBusPublisher = messageBusPublisher;

    /// <inheritdoc/>
    public async Task PublishAsync(Email email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _messageBusPublisher.PublishCommandAsync(CreateCommand(email), cancellationToken);
    }

    /// <summary>
    /// Creates a <see cref="SendEmailCommand"/> instance from the given <see cref="Email"/>.
    /// </summary>
    /// <param name="email">The email message to convert into a command.</param>
    /// <returns>A <see cref="SendEmailCommand"/> representing the email message.</returns>
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
