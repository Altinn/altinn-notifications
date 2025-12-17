using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Tools.EventGrid;

/// <summary>
/// Configuration settings for Event Grid client.
/// </summary>
public class EventGridSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
}

/// <summary>
/// HTTP client for posting events to Azure Event Grid.
/// </summary>
public class EventGridClient : IEventGridClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _accessKey;

    public EventGridClient(HttpClient httpClient, IOptions<EventGridSettings> settings)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        var cfg = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        if (string.IsNullOrWhiteSpace(cfg.BaseUrl)) throw new ArgumentException("BaseUrl must be configured", nameof(settings));
        _baseUrl = cfg.BaseUrl;
        _accessKey = cfg.AccessKey ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Posts an event to Event Grid.
    /// </summary>
    /// <param name="events">The array of events to post.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public async Task<(bool Success, string ResponseBody)> PostEventsAsync<T>(
        T[] events, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_baseUrl}?accesskey={_accessKey}";
            var content = new StringContent(
                JsonSerializer.Serialize(events), 
                Encoding.UTF8, 
                "application/json");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✓ Successfully posted {events.Length} event(s) to Event Grid");
                return (true, responseBody);
            }
            else
            {
                Console.WriteLine($"✗ Failed to post events. Status: {response.StatusCode}");
                Console.WriteLine($"Response: {responseBody}");
                return (false, responseBody);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error posting events: {ex.Message}");
            return (false, ex.Message);
        }
    }

}
