using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.Logging;

using Wolverine;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Wolverine-based implementation of <see cref="IEmailSendPublisher"/> that publishes
/// email notifications to an Azure Service Bus queue via <see cref="IMessageBus"/>.
/// </summary>
public class EmailSendPublisher : IEmailSendPublisher
{
    private readonly IMessageBus _messageBus;
    private readonly ILogger<EmailSendPublisher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSendPublisher"/> class.
    /// </summary>
    public EmailSendPublisher(ILogger<EmailSendPublisher> logger, IMessageBus messageBus)
    {
        _logger = logger;
        _messageBus = messageBus;
    }

    /// <inheritdoc/>
    public async Task<Guid?> PublishAsync(Email email, CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["NotificationId"] = email.NotificationId,
            ["ToAddress"] = email.ToAddress,
            ["Subject"] = email.Subject,
            ["Operation"] = "EmailPublish"
        });

        _logger.LogInformation(
            "EmailSendPublisher starting to publish email notification {NotificationId} to ASB queue. ToAddress: {ToAddress}, Subject: {Subject}",
            email.NotificationId,
            email.ToAddress,
            email.Subject);

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

        try
        {
            var startTime = DateTime.UtcNow;
            await _messageBus.SendAsync(sendEmailCommand);
            var duration = DateTime.UtcNow - startTime;

            _logger.LogInformation(
                "EmailSendPublisher successfully published email notification {NotificationId} to ASB queue in {Duration}ms. ToAddress: {ToAddress}",
                email.NotificationId,
                duration.TotalMilliseconds,
                email.ToAddress);

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "EmailSendPublisher failed to publish email notification {NotificationId} to ASB queue. ToAddress: {ToAddress}, Error: {ErrorMessage}",
                email.NotificationId,
                email.ToAddress,
                ex.Message);

            return email.NotificationId;
        }
    }
}
