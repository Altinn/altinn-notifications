using Altinn.Notifications.Triggers.Configuration;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Triggers.BackgroundServices
{
    /// <summary>
    /// Test class for a timer triggered background service
    /// </summary>
    public class TriggerTimer : IHostedService, IDisposable
    {
        private int executionCount = 0;
        private readonly ILogger<TriggerTimer> _logger;
        private Timer? _timer = null;
        private readonly string _baseUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="TriggerTimer"/> class.
        /// </summary>
        public TriggerTimer(ILogger<TriggerTimer> logger, IOptions<PlatformSettings> settings)
        {
            _logger = logger;
            _baseUrl = settings.Value.ApiNotificationsEndpoint;
        }

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Trigger Timer Hosted Service running.");

            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

            return Task.CompletedTask;
        }

        private void DoWork(object? state)
        {
            var count = Interlocked.Increment(ref executionCount);

            _logger.LogInformation(
                "Trigger Timer Service is working. Count: {Count}.  With value {_baseUrl}", count, _baseUrl);
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Timed Hosted Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}