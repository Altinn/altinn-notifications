using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Tools;

/// <summary>
/// HTTP client for posting events to Azure Event Grid.
/// </summary>
public class EventGridClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _accessKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventGridClient"/> class.
    /// </summary>
    /// <param name="baseUrl">The base URL for the Event Grid endpoint.</param>
    /// <param name="accessKey">The access key for authentication.</param>
    public EventGridClient(string baseUrl, string accessKey)
    {
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        _accessKey = accessKey ?? throw new ArgumentNullException(nameof(accessKey));
        
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
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

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
