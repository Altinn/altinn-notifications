using Altinn.Notifications.Core.BackgroundQueue;
using Altinn.Notifications.Core.Enums;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Base class for background services that run a dedicated processing loop per <see cref="SendingTimePolicy"/>.
/// Each loop waits for queued work, executes publishing, and then marks the policy as available.
/// </summary>
public abstract class SendingTimePolicyPublishBackgroundService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly ISendingTimePolicyTaskQueue _taskQueue;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendingTimePolicyPublishBackgroundService"/> class.
    /// </summary>
    /// <param name="taskQueue">The queue that signals work per sending time policy.</param>
    /// <param name="logger">The logger used to report errors from the processing loops.</param>
    protected SendingTimePolicyPublishBackgroundService(ISendingTimePolicyTaskQueue taskQueue, ILogger logger)
    {
        _logger = logger;
        _taskQueue = taskQueue;
    }

    /// <summary>
    /// Gets the name of the notification type published by this service, used in log messages (for example "SMS" or "email").
    /// </summary>
    protected abstract string NotificationType { get; }

    /// <summary>
    /// Claims and publishes pending notifications that are eligible under the given sending time policy.
    /// </summary>
    /// <param name="sendingTimePolicy">The sending time policy handled by the calling loop.</param>
    /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    protected abstract Task PublishAsync(SendingTimePolicy sendingTimePolicy, CancellationToken cancellationToken);

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
    /// The loop waits for queued work, executes publishing, and marks the policy as completed.
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
                await _taskQueue.WaitAsync(sendingTimePolicy, cancellationToken);
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
                await PublishAsync(sendingTimePolicy, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending {NotificationType} notifications for policy {Policy}.", NotificationType, sendingTimePolicy);
            }
            finally
            {
                _taskQueue.MarkCompleted(sendingTimePolicy);
            }
        }
    }
}
