using System.Diagnostics;
using Altinn.Notifications.Core.BackgroundQueue;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Background service that runs a dedicated processing loop.
/// Each loop cycle waits for queued work, executes email publishing, then marks is as available implicitly by calling wait, which will pop the item.
/// </summary>
public class EmailPublishBackgroundService : BackgroundService
{
    private static readonly ActivitySource _activitySource = new("Altinn.Notifications.Publish");
    private readonly IEmailPublishTaskQueue _emailPublishTaskQueue;
    private readonly IEmailNotificationService _emailNotificationService;
    private readonly ILogger<EmailPublishBackgroundService> _logger;

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
        _logger.LogError("RunPolicyLoopAsync debug init");
        while (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("RunPolicyLoopAsync debug start while");
            try
            {
                await _emailPublishTaskQueue.WaitAsync(cancellationToken);
                _logger.LogError("RunPolicyLoopAsync wait completed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("RunPolicyLoopAsync debug OperationCanceledException1");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while waiting for work.");
                continue;
            }

            try
            {
                using Activity? activity = _activitySource.StartActivity("EmailPublishBackgroundService.SendNotifications.Root");
                await _emailNotificationService.SendNotifications(cancellationToken);
                _logger.LogError("RunPolicyLoopAsync debug SendNotifications finished");
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("RunPolicyLoopAsync debug OperationCanceledException2");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending email notifications.");
            }

            _logger.LogError("RunPolicyLoopAsync debug end while");
        }
    }
}
