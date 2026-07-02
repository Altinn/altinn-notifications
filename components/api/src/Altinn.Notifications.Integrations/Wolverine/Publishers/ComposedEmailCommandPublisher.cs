using System.Collections.Concurrent;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Wolverine;

namespace Altinn.Notifications.Integrations.Wolverine.Publishers;

/// <summary>
/// Wolverine-based implementation of <see cref="IComposedEmailCommandPublisher"/> that publishes
/// composed email notifications to a dedicated Azure Service Bus queue via <see cref="IMessageBus"/>.
/// </summary>
public class ComposedEmailCommandPublisher(ILogger<ComposedEmailCommandPublisher> logger, IServiceProvider serviceProvider, IOptions<WolverineSettings> options) : IComposedEmailCommandPublisher
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<ComposedEmailCommandPublisher> _logger = logger;
    private readonly int _publishConcurrency = options.Value.ComposedEmailPublishConcurrency <= 0 ? 5 : options.Value.ComposedEmailPublishConcurrency;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ComposedEmail>> PublishAsync(IReadOnlyList<ComposedEmail> emails, CancellationToken cancellationToken)
    {
        if (emails.Count == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var failedEmails = new ConcurrentBag<ComposedEmail>();
        using var semaphore = new SemaphoreSlim(_publishConcurrency);

        await Task.WhenAll(emails.Select(async email =>
        {
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                var failedEmail = await SendAsync(email, messageBus, cancellationToken);
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
    /// Publishes a single composed email command to the message bus.
    /// </summary>
    /// <param name="email">The composed email to publish.</param>
    /// <param name="messageBus">The message bus used to send the command.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// <c>null</c> if the email was published successfully; otherwise, the <see cref="ComposedEmail"/> that failed to publish.
    /// </returns>
    private async Task<ComposedEmail?> SendAsync(ComposedEmail email, IMessageBus messageBus, CancellationToken cancellationToken)
    {
        var command = new SendComposedEmailCommand
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

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            await messageBus.SendAsync(command);

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
                "ComposedEmailCommandPublisher failed to publish composed email notification {NotificationId} to ASB queue.",
                email.NotificationId);

            return email;
        }
    }
}
