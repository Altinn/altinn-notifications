using System.Net;
using System.Text;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.ShortMessageService;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.ShortMessageService;

/// <summary>
/// HTTP Client for sending short text messages through the Altinn Notifications SMS service.
/// </summary>
public class ShortMessageServiceClient : IShortMessageServiceClient
{
    private readonly Uri _sendEndpoint;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ShortMessageServiceClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShortMessageServiceClient"/> class.
    /// </summary>
    public ShortMessageServiceClient(
        HttpClient httpClient,
        ILogger<ShortMessageServiceClient> logger,
        IOptions<PlatformSettings> platformSettings)
    {
        _logger = logger;
        _httpClient = httpClient;
        _sendEndpoint = new Uri("instantmessage/send", UriKind.Relative);
        _httpClient.BaseAddress = new Uri(platformSettings.Value.ApiShortMessageServiceEndpoint);
    }

    /// <inheritdoc/>
    public async Task<ShortMessageSendResult> SendAsync(ShortMessage shortMessage)
    {
        try
        {
            var content = new StringContent(shortMessage.Serialize(), Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(_sendEndpoint, content);

            if (response.IsSuccessStatusCode)
            {
                return new ShortMessageSendResult
                {
                    Success = true,
                    StatusCode = response.StatusCode
                };
            }
            else
            {
                string errorDetails = await response.Content.ReadAsStringAsync();

                _logger.LogWarning("Failed to send short message: {MessageContent}. Status: {StatusCode}, Details: {ErrorDetails}", content, response.StatusCode, errorDetails);

                return new ShortMessageSendResult
                {
                    Success = false,
                    ErrorDetails = errorDetails,
                    StatusCode = response.StatusCode,
                };
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed when sending short message");

            return new ShortMessageSendResult
            {
                Success = false,
                ErrorDetails = ex.Message,
                StatusCode = ex.StatusCode ?? HttpStatusCode.ServiceUnavailable,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when sending short message");

            return new ShortMessageSendResult
            {
                Success = false,
                StatusCode = HttpStatusCode.InternalServerError,
                ErrorDetails = $"An unexpected error occurred: {ex.Message}"
            };
        }
    }
}
