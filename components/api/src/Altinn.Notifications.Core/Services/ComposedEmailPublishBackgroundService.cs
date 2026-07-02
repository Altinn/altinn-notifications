using Altinn.Notifications.Core.BackgroundQueue;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Background service that drives the composed email publish loop.
/// Waits for a signal on <see cref="IComposedEmailPublishSignal"/>, then drains the composed email batch.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ComposedEmailPublishBackgroundService"/> class.
/// </remarks>
public class ComposedEmailPublishBackgroundService(
    IEmailNotificationService emailNotificationService,
    IComposedEmailPublishSignal composedEmailPublishSignal,
    ILogger<ComposedEmailPublishBackgroundService> logger) : BackgroundService
{
    private readonly ILogger<ComposedEmailPublishBackgroundService> _logger = logger;
    private readonly IEmailNotificationService _emailNotificationService = emailNotificationService;
    private readonly IComposedEmailPublishSignal _composedEmailPublishSignal = composedEmailPublishSignal;

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
                await _composedEmailPublishSignal.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while waiting for composed email publish work.");
                continue;
            }

            try
            {
                await _emailNotificationService.SendComposedNotifications(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending composed email notifications.");
            }
        }
    }
}
