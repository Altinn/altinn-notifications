using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Background service that sends email notifications using the <see cref="IEmailNotificationService"/>
/// </summary>
public class EmailNotificationsBackgroundService : BackgroundService
{
    private readonly ILogger<EmailNotificationsBackgroundService> _logger;
    private readonly IEmailNotificationService _emailSendingService;
    private readonly NotificationConfig _config;
    private readonly ManyNotificationsLately _manyEmailNotificationsLately = new();
    private readonly ManyNotificationsLately _manyComposedEmailNotificationsLately = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationsBackgroundService"/> class.
    /// </summary>
    public EmailNotificationsBackgroundService(IEmailNotificationService emailSendingService, IOptions<NotificationConfig> config, ILogger<EmailNotificationsBackgroundService> logger)
    {
        _emailSendingService = emailSendingService;
        _logger = logger;
        _config = config.Value;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_config.EmailNotificationsTaskCount == 0 && _config.ComposedEmailNotificationsTaskCount == 0)
        {
            // Unit test case: No tasks configured, so we don't start any loops. This is useful for unit tests that don't want to start background processing.
            // Could also be used in production to disable email notification processing if needed.
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Task[] emailNotificationTasks = Enumerable.Range(0, _config.EmailNotificationsTaskCount)
                    .Select(i => RunNotificationLoop(
                        isComposed: false,
                        isFirstInstance: i == 0,
                        stoppingToken))
                    .ToArray();

                Task[] composedEmailNotificationTask = Enumerable.Range(0, _config.ComposedEmailNotificationsTaskCount)
                    .Select(i => RunNotificationLoop(
                        isComposed: true,
                        isFirstInstance: i == 0,
                        stoppingToken))
                    .ToArray();

                await Task.WhenAll(emailNotificationTasks.Concat(composedEmailNotificationTask));
            }
            catch (Exception)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }

                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); // Wait before restarting
            }
        }
    }

    private async Task RunNotificationLoop(bool isComposed, bool isFirstInstance, CancellationToken stoppingToken)
    {
        int consecutiveRunsWithNotificationReturned = 0;
        var idleDelay = GetDelay(isComposed, isFirstInstance);
        int rampupLimit = isComposed ? _config.ComposedEmailNotificationsRampUpLimit : _config.EmailNotificationsRampUpLimit;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool shouldAttemptProcessing = isFirstInstance
                    || (isComposed ? _manyComposedEmailNotificationsLately.Get() : _manyEmailNotificationsLately.Get());
                bool shouldDelay = false;
                if (shouldAttemptProcessing)
                {
                    bool gotNotification = isComposed
                        ? await _emailSendingService.SendComposedNotification(stoppingToken)
                        : await _emailSendingService.SendNotification(stoppingToken);
                    if (!gotNotification && !stoppingToken.IsCancellationRequested)
                    {
                        consecutiveRunsWithNotificationReturned = 0;
                        shouldDelay = true;
                    }
                    else
                    {
                        consecutiveRunsWithNotificationReturned++;
                        if (consecutiveRunsWithNotificationReturned >= rampupLimit)
                        {
                            if (isComposed)
                            {
                                _manyComposedEmailNotificationsLately.Set(true);
                            }
                            else
                            {
                                _manyEmailNotificationsLately.Set(true);
                            }
                        }
                    }
                }
                else
                {
                    shouldDelay = true;
                }

                if (shouldDelay && !stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(idleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unhandled error in send notifications loop. IsComposed: {IsComposed}", isComposed);

                if (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(idleDelay, stoppingToken);
                }
            }
        }
    }

    private TimeSpan GetDelay(bool isComposed, bool isFirstInstance)
    {
        if (isFirstInstance)
        {
            return TimeSpan.FromSeconds(
                isComposed ? _config.ComposedEmailNotificationsPrimaryTaskIdleDelaySeconds : _config.EmailNotificationsPrimaryTaskIdleDelaySeconds);
        }
        else
        {
            return TimeSpan.FromSeconds(
                isComposed ? _config.ComposedEmailNotificationsAdditionalTasksIdleDelaySeconds : _config.EmailNotificationsAdditionalTasksIdleDelaySeconds);
        }
    }

    /// <summary>
    /// A thread-safe flag indicating whether there have been many notifications processed lately.
    /// </summary>
    internal class ManyNotificationsLately
    {
        private bool _manyNotificationsLately;
        private readonly object _lock = new();

        /// <summary>
        /// Gets the value of the flag in a thread-safe manner.
        /// </summary>
        /// <returns></returns>
        public bool Get()
        {
            lock (_lock)
            {
                return _manyNotificationsLately;
            }
        }

        /// <summary>
        /// Sets the value of the flag in a thread-safe manner.
        /// </summary>
        /// <param name="value">The value to set the flag to.</param>
        public void Set(bool value)
        {
            lock (_lock)
            {
                _manyNotificationsLately = value;
            }
        }
    }
}
