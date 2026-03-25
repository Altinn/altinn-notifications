using System.Collections.Concurrent;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Wolverine;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Wolverine-based implementation of <see cref="IEmailCommandPublisher"/> that publishes
/// email notifications to an Azure Service Bus queue via <see cref="IMessageBus"/>.
/// </summary>
public class EmailCommandPublisher(ILogger<EmailCommandPublisher> logger, IServiceProvider serviceProvider, IOptions<WolverineSettings> options) : IEmailCommandPublisher
{
    private readonly ILogger<EmailCommandPublisher> _logger = logger;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly int _publishConcurrency = options.Value.EmailPublishConcurrency <= 0 ? 10 : options.Value.EmailPublishConcurrency;

    /// <inheritdoc/>
    public async Task<Email?> PublishAsync(Email email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        return await SendAsync(email, messageBus);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Email>> PublishAsync(IReadOnlyList<Email> emails, CancellationToken cancellationToken)
    {
        if (emails.Count == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var failedEmails = new ConcurrentBag<Email>();
        using var semaphore = new SemaphoreSlim(_publishConcurrency);

        await Task.WhenAll(emails.Select(async email =>
        {
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                var failedEmail = await SendAsync(email, messageBus);
                if (failedEmail is not null)
                {
                    failedEmails.Add(failedEmail);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }));

        return [.. failedEmails];
    }

    /// <summary>
    /// Sends a single email notification command to the Azure Service Bus queue via <see cref="IMessageBus"/>.
    /// </summary>
    /// <param name="email">The email notification to send.</param>
    /// <param name="messageBus">The Wolverine message bus used to dispatch the command.</param>
    /// <returns>
    /// <see langword="null"/> if the command was dispatched successfully;
    /// otherwise the original <paramref name="email"/> if an error occurred.
    /// </returns>
    private async Task<Email?> SendAsync(Email email, IMessageBus messageBus)
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
                "EmailCommandPublisher failed to publish email notification {NotificationId} to ASB queue.",
                email.NotificationId);

            return email;
        }
    }
}
