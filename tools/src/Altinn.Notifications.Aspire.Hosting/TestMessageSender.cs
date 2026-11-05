using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Aspire.Hosting.ApplicationModel;

namespace Altinn.Notifications.Aspire.Hosting;

/// <summary>
/// Orders an instant SMS or email from the API as the service owner <c>ttd</c>, with a token from the mock token issuer.
/// </summary>
/// <param name="api">The API's endpoint.</param>
/// <param name="auth">The mock token issuer's endpoint.</param>
internal sealed class TestMessageSender(EndpointReference api, EndpointReference auth)
{
    private const string _scopes = "altinn:serviceowner/notifications.create";

    // The mock token issuer's default Basic auth credentials.
    private static readonly string _tokenCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("mock:mock"));

    public Task<ExecuteCommandResult> SendSmsAsync(HttpClient client, string recipient, string text, CancellationToken cancellationToken)
    {
        var order = new
        {
            idempotencyId = Guid.NewGuid().ToString(),
            sendersReference = "aspire-dashboard-" + Guid.NewGuid(),
            recipientSms = new
            {
                phoneNumber = recipient,
                timeToLiveInSeconds = 300,
                smsSettings = new { sender = "Altinn", body = text },
            },
        };

        return OrderAsync(client, "sms", order, "Phone", cancellationToken);
    }

    public Task<ExecuteCommandResult> SendEmailAsync(HttpClient client, string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        var order = new
        {
            idempotencyId = Guid.NewGuid().ToString(),
            sendersReference = "aspire-dashboard-" + Guid.NewGuid(),
            recipientEmail = new
            {
                emailAddress = recipient,
                emailSettings = new { subject, body, contentType = "Plain", senderEmailAddress = "noreply@altinn.no" },
            },
        };

        return OrderAsync(client, "email", order, "Inbox", cancellationToken);
    }

    private async Task<ExecuteCommandResult> OrderAsync(HttpClient client, string channel, object order, string tab, CancellationToken cancellationToken)
    {
        try
        {
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Get, $"{auth.Url}/api/GetEnterpriseToken?org=ttd&scopes={Uri.EscapeDataString(_scopes)}");
            tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", _tokenCredentials);
            using var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                return CommandResults.Failure($"The mock token issuer answered {(int)tokenResponse.StatusCode} to the token request.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{api.Url}/notifications/api/v1/future/orders/instant/{channel}")
            {
                Content = JsonContent.Create(order),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
            using var response = await client.SendAsync(request, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return CommandResults.Failure($"The API answered {(int)response.StatusCode}.", body, CommandResultFormat.Text);
            }

            return CommandResults.Success($"Ordered, shipment {ShipmentId(body) ?? "unknown"}. It shows up in the {tab} tab when sent.", body, CommandResultFormat.Json);
        }
        catch (HttpRequestException e)
        {
            return CommandResults.Failure($"Could not reach the API or the mock token issuer: {e.Message}");
        }
    }

    private static string? ShipmentId(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("notification", out var notification) && notification.TryGetProperty("shipmentId", out var id)
                ? id.ToString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
