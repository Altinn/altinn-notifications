namespace Altinn.Notifications.MockServices.Scheduling;

/// <summary>
/// Background service that periodically calls the Notifications API trigger endpoints
/// to process past-due orders and dispatch email/SMS notifications.
/// </summary>
public class TriggerScheduler : BackgroundService
{
    private static readonly string[] _triggerPaths =
    [
        "/notifications/api/v1/trigger/pastdueorders",
        "/notifications/api/v1/trigger/sendemail",
        "/notifications/api/v1/trigger/sendsmsanytime",
    ];

    private readonly ILogger<TriggerScheduler> _logger;
    private readonly HttpClient _httpClient;

    public TriggerScheduler(ILogger<TriggerScheduler> logger, IConfiguration configuration)
    {
        _logger = logger;
        string baseUrl = configuration["TriggerScheduler:ApiBaseUrl"] ?? "http://localhost:5090";
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(10) };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TriggerScheduler started — will POST to trigger endpoints every 5 seconds.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (string path in _triggerPaths)
            {
                try
                {
                    using var response = await _httpClient.PostAsync(path, null, stoppingToken);
                    _logger.LogInformation("TriggerScheduler: POST {Path} → {StatusCode}", path, (int)response.StatusCode);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning("TriggerScheduler: POST {Path} failed (API not ready?): {Message}", path, ex.Message);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("TriggerScheduler: POST {Path} timed out.", path);
                }
            }
        }
    }

    public override void Dispose()
    {
        _httpClient.Dispose();
        base.Dispose();
    }
}
