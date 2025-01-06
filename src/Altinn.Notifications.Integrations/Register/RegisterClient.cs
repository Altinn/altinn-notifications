using System.Text;
using System.Text.Json;

using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Core.Models.Parties;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.Configuration;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterClient"/> class.
    /// </summary>
    /// <param name="client">The HTTP client used to make requests to the register service.</param>
    /// <param name="settings">The platform settings containing the API endpoints.</param>
    public RegisterClient(HttpClient client, IOptions<PlatformSettings> settings)
    {
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

        return await PostAsync<List<OrganizationContactPoints>>(_contactPointLookupEndpoint, lookupObject);
    }

    /// <inheritdoc/>
    public async Task<List<PartyDetails>> GetPartyDetails(List<string> organizationNumbers, List<string> socialSecurityNumbers)
    {
        if ((organizationNumbers?.Count ?? 0) == 0 && (socialSecurityNumbers?.Count ?? 0) == 0)
        {
            return [];
        }

        var partyDetailsLookupBatch = new PartyDetailsLookupBatch(organizationNumbers, socialSecurityNumbers);

        return await PostAsync<List<PartyDetails>>(_nameComponentsLookupEndpoint, partyDetailsLookupBatch);
    }

    /// <inheritdoc/>
    public async Task<List<PartyDetails>> GetPartyDetailsForOrganizations(List<string> organizationNumbers)
    {
        if (organizationNumbers == null || organizationNumbers.Count == 0)
        {
            return [];
        }

        var partyDetailsLookupBatch = new PartyDetailsLookupBatch(organizationNumbers: organizationNumbers);

        return await PostAsync<List<PartyDetails>>(_nameComponentsLookupEndpoint, partyDetailsLookupBatch);
    }

    /// <inheritdoc/>
    public async Task<List<PartyDetails>> GetPartyDetailsForPersons(List<string> socialSecurityNumbers)
    {
        if (socialSecurityNumbers == null || socialSecurityNumbers.Count == 0)
        {
            return [];
        }

        var partyDetailsLookupBatch = new PartyDetailsLookupBatch(socialSecurityNumbers: socialSecurityNumbers);

        return await PostAsync<List<PartyDetails>>(_nameComponentsLookupEndpoint, partyDetailsLookupBatch);
    }

    /// <summary>
    /// Sends a POST request to the specified endpoint with the given payload and deserializes the response to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to which the response content should be deserialized.</typeparam>
    /// <param name="endpoint">The endpoint to which the POST request should be sent.</param>
    /// <param name="payload">The payload to be sent in the POST request.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the deserialized response content.</returns>
    /// <exception cref="PlatformHttpException">Thrown when the HTTP response indicates a failure.</exception>
    /// <exception cref="InvalidOperationException">Thrown when deserialization of the response content fails.</exception>
    private async Task<T> PostAsync<T>(string endpoint, object payload)
    {
        HttpContent content = new StringContent(JsonSerializer.Serialize(payload, _jsonSerializerOptions), Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(endpoint, content);

        if (!response.IsSuccessStatusCode)
        {
            throw await PlatformHttpException.CreateAsync(response);
        }

        string responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(responseContent, _jsonSerializerOptions) ?? throw new InvalidOperationException("Deserialization failed");
    }
}
