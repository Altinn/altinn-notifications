using Altinn.Notifications.Core.BackgroundQueue;
using Altinn.Notifications.Core.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Background service that runs a dedicated processing loop.
/// Each loop waits for queued work, executes email publishing, then marks is as available.
/// </summary>
public class EmailPublishBackgroundService : BackgroundService
{
    private readonly IEmailPublishTaskQueue _emailPublishTaskQueue;
    private readonly IEmailNotificationService _emailNotificationService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailPublishBackgroundService"/> class.
    /// </summary>
    public EmailPublishBackgroundService(IEmailPublishTaskQueue emailPublishTaskQueue, IEmailNotificationService emailNotificationService, ILogger<EmailPublishBackgroundService> logger)
    {
        _emailPublishTaskQueue = emailPublishTaskQueue;
        _emailNotificationService = emailNotificationService;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RunPolicyLoopAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }

    private async Task RunPolicyLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _emailPublishTaskQueue.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while waiting for work.");
                continue;
            }

            try
            {
                await _emailNotificationService.SendNotifications();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending email notifications.");
            }
            finally
            {
                _emailPublishTaskQueue.MarkCompleted();
            }
        }
    }
}
