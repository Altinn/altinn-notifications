using Altinn.Notifications.Core.BackgroundQueue;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Background worker that pulls <see cref="SendingTimePolicy"/> work items from the queue
/// and invokes <see cref="ISmsNotificationService"/> to publish eligible SMS notifications.
/// </summary>
public class SmsSendBackgroundService : BackgroundService
{
    private readonly ILogger<SmsSendBackgroundService> _logger;
    private readonly ISmsSendBackgroundQueue _sendBackgroundQueue;
    private readonly ISmsNotificationService _smsNotificationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsSendBackgroundService"/> class.
    /// </summary>
    public SmsSendBackgroundService(
        ILogger<SmsSendBackgroundService> logger,
        ISmsSendBackgroundQueue sendBackgroundQueue,
        ISmsNotificationService smsNotificationService)
    {
        _logger = logger;
        _sendBackgroundQueue = sendBackgroundQueue;
        _smsNotificationService = smsNotificationService;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            SendingTimePolicy sendingTimePolicy;
            try
            {
                sendingTimePolicy = await _sendBackgroundQueue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed while dequeuing SMS send work item.");
                continue;
            }

            try
            {
                await _smsNotificationService.SendNotifications(stoppingToken, sendingTimePolicy);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while sending SMS notifications for policy {Policy}.", sendingTimePolicy);
            }
            finally
            {
                _sendBackgroundQueue.MarkCompleted(sendingTimePolicy);
            }
        }
    }
}
