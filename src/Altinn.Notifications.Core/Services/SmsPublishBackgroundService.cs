using Altinn.Notifications.Core.BackgroundQueue;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Background service that runs a dedicated processing loop per <see cref="SendingTimePolicy"/>.
/// 
/// Each loop waits for queued work, executes SMS publishing, and then marks the policy as available.
/// </summary>
public class SmsPublishBackgroundService : BackgroundService
{
    private readonly ISmsPublishTaskQueue _smsPublishTaskQueue;
    private readonly ILogger<SmsPublishBackgroundService> _logger;
    private readonly ISmsNotificationService _smsNotificationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsPublishBackgroundService"/> class.
    /// </summary>
    public SmsPublishBackgroundService(
        ISmsPublishTaskQueue smsPublishTaskQueue,
        ILogger<SmsPublishBackgroundService> logger,
        ISmsNotificationService smsNotificationService)
    {
        _logger = logger;
        _smsPublishTaskQueue = smsPublishTaskQueue;
        _smsNotificationService = smsNotificationService;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var anytimeLoop = RunPolicyLoopAsync(SendingTimePolicy.Anytime, stoppingToken);
        var daytimeLoop = RunPolicyLoopAsync(SendingTimePolicy.Daytime, stoppingToken);

        try
        {
            await Task.WhenAll(anytimeLoop, daytimeLoop);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }

    /// <summary>
    /// Runs a continuous processing loop for the specified <see cref="SendingTimePolicy"/>.
    /// The loop waits for queued work, executes SMS publishing, and marks the policy as completed.
    /// </summary>
    /// <param name="sendingTimePolicy">The sending time policy handled by this loop.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests and stop the loop gracefully.</param>
    /// <returns>A task representing the asynchronous loop execution.</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the loop or any awaited operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    private async Task RunPolicyLoopAsync(SendingTimePolicy sendingTimePolicy, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _smsPublishTaskQueue.WaitAsync(sendingTimePolicy, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while waiting for work for policy {Policy}.", sendingTimePolicy);
                continue;
            }

            try
            {
                await _smsNotificationService.SendNotifications(cancellationToken, sendingTimePolicy);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending SMS notifications for policy {Policy}.", sendingTimePolicy);
            }
            finally
            {
                _smsPublishTaskQueue.MarkCompleted(sendingTimePolicy);
            }
        }
    }
}
