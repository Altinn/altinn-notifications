using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Wolverine;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Wolverine-based implementation of <see cref="IEmailCommandPublisher"/> that publishes
/// email notifications to an Azure Service Bus queue via <see cref="IMessageBus"/>.
/// </summary>
public class EmailCommandPublisher : IEmailCommandPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailCommandPublisher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailCommandPublisher"/> class.
    /// </summary>
    public EmailCommandPublisher(ILogger<EmailCommandPublisher> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task<Guid?> PublishAsync(Email email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        return await SendAsync(email, messageBus);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Guid>> PublishAsync(IReadOnlyList<Email> emails, CancellationToken cancellationToken)
    {
        if (emails.Count == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var failedNotificationIds = new List<Guid>();

        foreach (var email in emails)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var failedId = await SendAsync(email, messageBus);
            if (failedId.HasValue)
            {
                failedNotificationIds.Add(failedId.Value);
            }
        }

        return failedNotificationIds;
    }

    private async Task<Guid?> SendAsync(Email email, IMessageBus messageBus)
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

        try
        {
            await messageBus.SendAsync(sendEmailCommand);

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
                "EmailCommandPublisher failed to publish email notification {NotificationId} to ASB queue. ToAddress: {ToAddress}",
                email.NotificationId,
                email.ToAddress);

            return email.NotificationId;
        }
    }
}
