using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Shared.Commands;

using Wolverine;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Wolverine-based implementation of <see cref="IEmailSendPublisher"/> that publishes
/// email notifications to an Azure Service Bus queue via <see cref="IMessageBus"/>.
/// </summary>
public class EmailSendPublisher : IEmailSendPublisher
{
    private readonly IMessageBus _messageBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSendPublisher"/> class.
    /// </summary>
    /// <param name="messageBus">The Wolverine message bus.</param>
    public EmailSendPublisher(IMessageBus messageBus)
    {
        _messageBus = messageBus;
    }

    /// <inheritdoc/>
    public async Task<Guid?> PublishAsync(Email email, CancellationToken cancellationToken)
    {
        try
        {
            var sendEmailCommand = new SendEmailCommand
            {
                Body = email.Body,
                Subject = email.Subject,
                ToAddress = email.ToAddress,
                FromAddress = email.FromAddress,
                NotificationId = email.NotificationId,
                ContentType = email.ContentType.ToString()
            };

            cancellationToken.ThrowIfCancellationRequested();

            await _messageBus.SendAsync(sendEmailCommand);

            return null;
        }
        catch (Exception)
        {
            return email.NotificationId;
        }
    }
}
