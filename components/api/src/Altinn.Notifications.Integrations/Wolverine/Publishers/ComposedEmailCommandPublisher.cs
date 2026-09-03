using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Publishers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Wolverine.Publishers;

/// <summary>
/// Wolverine-based implementation of <see cref="IComposedEmailCommandPublisher"/> that publishes
/// composed email notifications to a dedicated Azure Service Bus queue via <see cref="IMessageBusPublisher"/>.
/// </summary>
public class ComposedEmailCommandPublisher(ILogger<ComposedEmailCommandPublisher> logger, IMessageBusPublisher messageBusPublisher, IOptions<WolverineSettings> options) : IComposedEmailCommandPublisher
{
    private readonly ILogger<ComposedEmailCommandPublisher> _logger = logger;
    private readonly IMessageBusPublisher _messageBusPublisher = messageBusPublisher;
    private readonly int _publishConcurrency = options.Value.ComposedEmailPublishConcurrency;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ComposedEmail>> PublishAsync(IReadOnlyList<ComposedEmail> emails, CancellationToken cancellationToken)
    {
        if (emails.Count == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        return await _messageBusPublisher.PublishBatchAsync(
            emails,
            CreateCommand,
            OnPublishError,
            cancellationToken);
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

    /// <summary>
    /// Logs an error for a composed email that failed to publish.
    /// </summary>
    /// <param name="email">The composed email that failed to publish.</param>
    /// <param name="ex">The exception raised during the publish attempt.</param>
    private void OnPublishError(ComposedEmail email, Exception ex)
    {
        _logger.LogError(
            ex,
            "ComposedEmailCommandPublisher failed to publish composed email notification {NotificationId} to ASB queue.",
            email.NotificationId);
    }
}
