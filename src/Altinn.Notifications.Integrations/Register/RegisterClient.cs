using System.Text;
using System.Text.Json;

using Altinn.Common.AccessTokenClient.Services;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Core.Models.Parties;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Register.Models;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Register;

/// <summary>
/// Implementation of the <see cref="IRegisterClient"/> to retrieve information for organizations and individuals.
/// </summary>
public class RegisterClient : IRegisterClient
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly string _contactPointLookupEndpoint = "organizations/contactpoint/lookup";
    private readonly string _nameComponentsLookupEndpoint = "parties/nameslookup";

    private readonly IAccessTokenGenerator _accessTokenGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterClient"/> class.
    /// </summary>
    /// <param name="client">The HTTP client used to make requests to the register service.</param>
    /// <param name="settings">The platform settings containing the API endpoints.</param>
    /// <param name="accessTokenGenerator">The access token generator.</param>
    public RegisterClient(HttpClient client, IOptions<PlatformSettings> settings, IAccessTokenGenerator accessTokenGenerator)
    {
        _accessTokenGenerator = accessTokenGenerator;

        _client = client;
        _client.BaseAddress = new Uri(settings.Value.ApiRegisterEndpoint);

        _jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    /// <inheritdoc/>
    public async Task<List<OrganizationContactPoints>> GetOrganizationContactPoints(List<string> organizationNumbers)
    {
        if (organizationNumbers == null || organizationNumbers.Count == 0)
        {
            return [];
        }

        var lookupObject = new OrgContactPointLookup
        {
            OrganizationNumbers = organizationNumbers
        };

        var content = CreateJsonContent(lookupObject);

        var response = await _client.PostAsync(_contactPointLookupEndpoint, content);

        if (!response.IsSuccessStatusCode)
        {
            throw await PlatformHttpException.CreateAsync(response);
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var contactPoints = JsonSerializer.Deserialize<OrgContactPointsList>(responseContent, _jsonSerializerOptions)?.ContactPointsList;
        return contactPoints ?? [];
    }

    /// <inheritdoc/>
    public async Task<List<PartyDetails>> GetPartyDetails(List<string>? organizationNumbers = null, List<string>? socialSecurityNumbers = null)
    {
        if ((organizationNumbers?.Count ?? 0) == 0 && (socialSecurityNumbers?.Count ?? 0) == 0)
        {
            return [];
        }

        HttpRequestMessage requestMessage = new(HttpMethod.Post, _nameComponentsLookupEndpoint)
        {
            Content = CreateJsonContent(new PartyDetailsLookupBatch(organizationNumbers, socialSecurityNumbers))
        };

        var accessToken = _accessTokenGenerator.GenerateAccessToken("platform", "notification");
        if (!string.IsNullOrEmpty(accessToken))
        {
            requestMessage.Headers.Add("PlatformAccessToken", accessToken);
        }

        var response = await _client.SendAsync(requestMessage, CancellationToken.None);

        if (!response.IsSuccessStatusCode)
        {
            throw await PlatformHttpException.CreateAsync(response);
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var partyDetailsList = JsonSerializer.Deserialize<PartyDetailsLookupResult>(responseContent, _jsonSerializerOptions)?.PartyDetailsList;

        return partyDetailsList ?? [];
    }

    /// <summary>
    /// Creates an HTTP content object with a JSON-serialized representation of the specified object.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="payload">The object to serialize into JSON.</param>
    /// <returns>
    /// An <see cref="HttpContent"/> object containing the serialized JSON representation 
    /// of the provided object, encoded in UTF-8, with a media type of "application/json".
    /// </returns>
    /// <remarks>
    /// This method uses the specified <see cref="JsonSerializerOptions"/> to control the serialization behavior.
    /// </remarks>
    private StringContent CreateJsonContent<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload, _jsonSerializerOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
