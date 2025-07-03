using System.Text;
using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Clients;

/// <summary>
/// Implementation of the <see cref="INotificationsSmsClient"/>
/// </summary>
public class NotificationsSmsClient : INotificationsSmsClient
{
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileClient"/> class.
    /// </summary>
    public NotificationsSmsClient(HttpClient client, IOptions<PlatformSettings> settings)
    {
        _client = client;
        _client.BaseAddress = new Uri(settings.Value.ApiNotificationsSmsEndpoint);
    }

    /// <inheritdoc/>
    public async Task<bool> Send(InstantSmsPayload instantSmsPayload)
    {
        HttpContent content = new StringContent(JsonSerializer.Serialize(instantSmsPayload, JsonSerializerOptionsProvider.Options), Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("instantmessage/send", content);

        if (!response.IsSuccessStatusCode)
        {
            throw new PlatformHttpException(response, $"NotificationsSmsClient.Send failed with status code {response.StatusCode}");
        }

        return response.IsSuccessStatusCode;
    }
}
