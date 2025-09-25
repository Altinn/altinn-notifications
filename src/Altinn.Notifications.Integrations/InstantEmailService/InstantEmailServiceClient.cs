using System.Net;
using System.Text;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.InstantEmailService;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.InstantEmailService;

/// <summary>
/// HTTP Client for sending instant emails through the Altinn Notifications Email service.
/// </summary>
public class InstantEmailServiceClient : IInstantEmailServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InstantEmailServiceClient> _logger;
    private readonly Uri _sendEndpoint = new("instantemail", UriKind.Relative);

    /// <summary>
    /// Initializes a new instance of the <see cref="InstantEmailServiceClient"/> class.
    /// </summary>
    public InstantEmailServiceClient(
        HttpClient httpClient,
        ILogger<InstantEmailServiceClient> logger,
        IOptions<PlatformSettings> platformSettings)
    {
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(platformSettings.Value.ApiInstantEmailServiceEndpoint);
    }

    /// <inheritdoc/>
    public async Task<InstantEmailSendResult> SendAsync(InstantEmail instantEmail)
    {
        try
        {
            var serializedInstantEmail = instantEmail.Serialize();
            var content = new StringContent(serializedInstantEmail, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(_sendEndpoint, content);

            if (response.IsSuccessStatusCode)
            {
                return new InstantEmailSendResult
                {
                    Success = true,
                    StatusCode = response.StatusCode
                };
            }
            else
            {
                string errorDetails = await response.Content.ReadAsStringAsync();

                _logger.LogWarning("Failed to send instant email: {EmailContent}. Status: {StatusCode}, Details: {ErrorDetails}", serializedInstantEmail, response.StatusCode, errorDetails);

                return new InstantEmailSendResult
                {
                    Success = false,
                    ErrorDetails = errorDetails,
                    StatusCode = response.StatusCode,
                };
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed when sending instant email");

            return new InstantEmailSendResult
            {
                Success = false,
                ErrorDetails = ex.Message,
                StatusCode = ex.StatusCode ?? HttpStatusCode.ServiceUnavailable,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when sending instant email");

            return new InstantEmailSendResult
            {
                Success = false,
                ErrorDetails = $"An unexpected error occurred: {ex.Message}",
                StatusCode = HttpStatusCode.InternalServerError
            };
        }
    }
}
