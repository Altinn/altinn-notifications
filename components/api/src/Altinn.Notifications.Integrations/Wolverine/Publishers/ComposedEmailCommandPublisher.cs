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
    private readonly int _publishConcurrency = options.Value.ComposedEmailPublishConcurrency;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ComposedEmail>> PublishAsync(IReadOnlyList<ComposedEmail> emails, CancellationToken cancellationToken)
    {
        if (emails.Count == 0)
        {
            return [];
        }

        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var unpublished = new ConcurrentBag<ComposedEmail>();
        using var semaphore = new SemaphoreSlim(_publishConcurrency);

        await Task.WhenAll(emails.Select(async email =>
        {
            try
            {
                // Cancellation is only observed before acquiring a slot.
                // Once a send has started, it is allowed to complete to avoid
                // an ambiguous published/not-published state on the broker.
                await semaphore.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                unpublished.Add(email);

                return;
            }

            try
            {
                if (!await SendAsync(email, messageBus))
                {
                    unpublished.Add(email);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }));

        return [.. unpublished];
    }

    /// <summary>
    /// Publishes a single composed email command to the message bus.
    /// </summary>
    /// <param name="email">The composed email to publish.</param>
    /// <param name="messageBus">The message bus used to send the command.</param>
    /// <returns>
    /// <c>true</c> if the email was published successfully; <c>false</c> if publish operation did not complete successfully.
    /// </returns>
    private async Task<bool> SendAsync(ComposedEmail email, IMessageBus messageBus)
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
            await messageBus.SendAsync(command);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "ComposedEmailCommandPublisher failed to publish composed email notification {NotificationId} to ASB queue.",
                email.NotificationId);

            return false;
        }
    }
}
