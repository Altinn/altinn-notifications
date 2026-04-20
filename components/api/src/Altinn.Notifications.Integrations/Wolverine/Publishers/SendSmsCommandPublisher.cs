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
/// Wolverine-based implementation of <see cref="ISendSmsPublisher"/> that publishes
/// SMS notifications to an Azure Service Bus queue via <see cref="IMessageBus"/>.
/// </summary>
/// <remarks>
/// This class implements the <see cref="ISendSmsPublisher"/> interface to enable asynchronous publication of SMS
/// commands to Azure Service Bus via Wolverine. Ensure that the provided <see cref="Sms"/> object is properly configured before calling PublishAsync.
/// </remarks>
/// <param name="logger">The logger used to record operational events and errors during SMS publishing.</param>
/// <param name="serviceProvider">The service provider used to resolve dependencies required for publishing SMS messages.</param>
/// <param name="options">Configuration options for Wolverine settings, including SMS publish concurrency.</param>
public class SendSmsCommandPublisher(ILogger<SendSmsCommandPublisher> logger, IServiceProvider serviceProvider, IOptions<WolverineSettings> options) : ISendSmsPublisher
{
    private readonly ILogger<SendSmsCommandPublisher> _logger = logger;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly int _publishConcurrency = options.Value.SmsPublishConcurrency <= 0 ? 10 : options.Value.SmsPublishConcurrency;

    /// <inheritdoc/>
    public async Task<Sms?> PublishAsync(Sms sms, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        return await SendAsync(sms, messageBus);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Sms>> PublishAsync(IReadOnlyList<Sms> smsList, CancellationToken cancellationToken)
    {
        if (smsList.Count == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var failedSms = new ConcurrentBag<Sms>();
        using var semaphore = new SemaphoreSlim(_publishConcurrency);

        await Task.WhenAll(smsList.Select(async sms =>
        {
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var failedMessage = await SendAsync(sms, messageBus);
                if (failedMessage is not null)
                {
                    failedSms.Add(failedMessage);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }));

        return [.. failedSms];
    }

    /// <summary>
    /// Sends a single SMS notification command to the Azure Service Bus queue via <see cref="IMessageBus"/>.
    /// </summary>
    /// <param name="sms">The SMS notification to send.</param>
    /// <param name="messageBus">The Wolverine message bus used to dispatch the command.</param>
    /// <returns>
    /// <see langword="null"/> if the command was dispatched successfully;
    /// otherwise the original <paramref name="sms"/> if an error occurred.
    /// </returns>
    private async Task<Sms?> SendAsync(Sms sms, IMessageBus messageBus)
    {
        var sendSmsCommand = new SendSmsCommand
        {
            MobileNumber = sms.Recipient,
            Body = sms.Message,
            SenderNumber = sms.Sender,
            NotificationId = sms.NotificationId
        };

        try
        {
            await messageBus.SendAsync(sendSmsCommand);

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
                "SendSmsCommandPublisher failed to publish SMS notification {NotificationId} to ASB queue.",
                sms.NotificationId);

            return sms;
        }
    }
}
