using System.Text;
using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Profile;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Clients;

/// <summary>
/// Implementation of the <see cref="IProfileClient"/>
/// </summary>
public class ProfileClient : IProfileClient
{
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileClient"/> class.
    /// </summary>
    public ProfileClient(HttpClient client, IOptions<PlatformSettings> settings)
    {
        _client = client;
        _client.BaseAddress = new Uri(settings.Value.ApiProfileEndpoint);
    }

    /// <inheritdoc/>
    public async Task<List<UserContactPoints>> GetUserContactPoints(List<string> nationalIdentityNumbers)
    {
        var lookupObject = new UserContactPointLookup
        {
            NationalIdentityNumbers = nationalIdentityNumbers
        };

        HttpContent content = new StringContent(JsonSerializer.Serialize(lookupObject, JsonSerializerOptionsProvider.Options), Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("users/contactpoint/lookup", content);

        if (!response.IsSuccessStatusCode)
        {
            throw new PlatformHttpException(response, $"ProfileClient.GetUserContactPoints failed with status code {response.StatusCode}");
        }

        string responseContent = await response.Content.ReadAsStringAsync();
        List<UserContactPoints>? contactPoints = JsonSerializer.Deserialize<UserContactPointsList>(responseContent, JsonSerializerOptionsProvider.Options)!.ContactPointList;
        return contactPoints!;
    }
}
