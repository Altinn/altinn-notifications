using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Exceptions;
using Altinn.Notifications.Shared.Publishers;

namespace Altinn.Notifications.Integrations.Wolverine.Publishers;

/// <summary>
/// Wolverine-based implementation of <see cref="IComposedEmailCommandPublisher"/> that publishes
/// composed email notifications to a dedicated Azure Service Bus queue via <see cref="IWolverinePublisher"/>.
/// </summary>
public class ComposedEmailCommandPublisher(IWolverinePublisher wolverinePublisher) : IComposedEmailCommandPublisher
{
    /// <inheritdoc/>
    public async Task PublishAsync(ComposedEmail email, CancellationToken cancellationToken)
    {
        try
        {
            await wolverinePublisher.PublishCommandAsync(CreateCommand(email), cancellationToken);
        }
        catch (ContentTooLargeException ex)
        {
            throw new NotificationSizeLimitExceededException($"Composed email {email.NotificationId} exceeds the message queue size limit.", ex);
        }
    }

    /// <summary>
    /// Creates a <see cref="SendComposedEmailCommand"/> from a <see cref="ComposedEmail"/>.
    /// </summary>
    /// <param name="email">The composed email to convert.</param>
    /// <returns>The command to publish to the message bus.</returns>
    private static SendComposedEmailCommand CreateCommand(ComposedEmail email)
    {
        return new SendComposedEmailCommand
        {
            Body = email.Body,
            Subject = email.Subject,
            ToAddress = email.ToAddress,
            FromAddress = email.FromAddress,
            NotificationId = email.NotificationId,
            ContentType = email.ContentType.ToString(),
            Attachments = [.. email.Attachments
                .Select(a => new SasFileAttachment
                {
                    Filename = a.Filename,
                    MimeType = a.MimeType,
                    SasUrl = a.SasUrl.ToString()
                })]
        };
    }
}
