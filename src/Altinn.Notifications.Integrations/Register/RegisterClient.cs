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
    private readonly string _nameComponentsLookupEndpoint = "parties/nameslookup";
    private readonly string _contactPointLookupEndpoint = "organizations/contactpoint/lookup";

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

    /// <summary>
    /// Asynchronously retrieves contact point details for the specified organizations.
    /// </summary>
    /// <param name="organizationNumbers">A collection of organization numbers for which contact point details are requested.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains a list of <see cref="OrganizationContactPoints" /> representing the contact points of the specified organizations.
    /// </returns>
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

        HttpContent content = new StringContent(JsonSerializer.Serialize(lookupObject, _jsonSerializerOptions), Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(_contactPointLookupEndpoint, content);

        if (!response.IsSuccessStatusCode)
        {
            throw await PlatformHttpException.CreateAsync(response);
        }

        string responseContent = await response.Content.ReadAsStringAsync();
        var contactPoints = JsonSerializer.Deserialize<OrgContactPointsList>(responseContent, _jsonSerializerOptions)?.ContactPointsList;
        return contactPoints ?? [];
    }

    /// <summary>
    /// Asynchronously retrieves party details for the specified organizations.
    /// </summary>
    /// <param name="organizationNumbers">A collection of organization numbers for which party details are requested.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains a list of <see cref="PartyDetails" /> representing the details of the specified organizations.
    /// </returns>
    public async Task<List<PartyDetails>> GetPartyDetailsForOrganizations(List<string> organizationNumbers)
    {
        if (organizationNumbers == null || organizationNumbers.Count == 0)
        {
            return [];
        }

        var partyDetailsLookupBatch = new PartyDetailsLookupBatch
        {
            PartyDetailsLookupRequestList = organizationNumbers.Select(orgNumber => new PartyDetailsLookupRequest { OrganizationNumber = orgNumber }).ToList()
        };

        HttpContent content = new StringContent(JsonSerializer.Serialize(partyDetailsLookupBatch), Encoding.UTF8, "application/json");

        var response = await _client.PostAsync($"{_nameComponentsLookupEndpoint}", content);

        if (!response.IsSuccessStatusCode)
        {
            throw await PlatformHttpException.CreateAsync(response);
        }

        string responseContent = await response.Content.ReadAsStringAsync();
        var partyNamesLookupResponse = JsonSerializer.Deserialize<PartyDetailsLookupResult>(responseContent, _jsonSerializerOptions);
        return partyNamesLookupResponse?.PartyDetailsList ?? [];
    }

    /// <summary>
    /// Asynchronously retrieves party details for the specified persons.
    /// </summary>
    /// <param name="socialSecurityNumbers">A collection of social security numbers for which party details are requested.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains a list of <see cref="PartyDetails" /> representing the details of the specified individuals.
    /// </returns>
    public async Task<List<PartyDetails>> GetPartyDetailsForPersons(List<string> socialSecurityNumbers)
    {
        if (socialSecurityNumbers == null || socialSecurityNumbers.Count == 0)
        {
            return [];
        }

        var partyDetailsLookupBatch = new PartyDetailsLookupBatch
        {
            PartyDetailsLookupRequestList = socialSecurityNumbers.Select(ssn => new PartyDetailsLookupRequest { SocialSecurityNumber = ssn }).ToList()
        };

        HttpContent content = new StringContent(JsonSerializer.Serialize(partyDetailsLookupBatch), Encoding.UTF8, "application/json");

        var response = await _client.PostAsync($"{_nameComponentsLookupEndpoint}?partyComponentOption=person-name", content);

        if (!response.IsSuccessStatusCode)
        {
            throw await PlatformHttpException.CreateAsync(response);
        }

        string responseContent = await response.Content.ReadAsStringAsync();
        var partyNamesLookupResponse = JsonSerializer.Deserialize<PartyDetailsLookupResult>(responseContent, _jsonSerializerOptions);
        return partyNamesLookupResponse?.PartyDetailsList ?? [];
    }
}
