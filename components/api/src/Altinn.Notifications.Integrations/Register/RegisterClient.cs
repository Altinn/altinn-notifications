using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Common.AccessTokenClient.Services;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.Parties;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.Register.Models;
using Altinn.Register.Contracts;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Register;

/// <summary>
/// A client implementation of <see cref="IRegisterClient"/> for retrieving information about organizations and individuals.
/// </summary>
public class RegisterClient : IRegisterClient
{
    private const int _partyQueryMaxItems = 100;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly string _nameComponentsLookupEndpoint = "v1/parties/nameslookup";
    private readonly string _partiesQueryEndpoint = "v2/internal/parties/query";

    private readonly IAccessTokenGenerator _accessTokenGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterClient"/> class.
    /// </summary>
    /// <param name="client">The HTTP client used to make requests to the register service.</param>
    /// <param name="settings">The register settings containing the API endpoint.</param>
    /// <param name="accessTokenGenerator">The access token generator.</param>
    public RegisterClient(HttpClient client, IOptions<RegisterSettings> settings, IAccessTokenGenerator accessTokenGenerator)
    {
        _client = client;
        _client.BaseAddress = new Uri(settings.Value.ApiEndpoint);

        _accessTokenGenerator = accessTokenGenerator;

        _jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    /// <inheritdoc/>
    public async Task<List<PartyDetails>> QueryPartyDetails(
        IReadOnlyCollection<string> organizationNumbers,
        IReadOnlyCollection<string> nationalIdentityNumbers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizationNumbers);
        ArgumentNullException.ThrowIfNull(nationalIdentityNumbers);

        var partyUrns = CreatePartyUrns(organizationNumbers, nationalIdentityNumbers);
        var partyDetails = new List<PartyDetails>();

        foreach (var batch in partyUrns.Chunk(_partyQueryMaxItems))
        {
            var parties = await GetParties(batch, cancellationToken);
            partyDetails.AddRange(parties.Select(ToPartyDetails).OfType<PartyDetails>());
        }

        return partyDetails;
    }

    private async Task<List<Party>> GetParties(Altinn.Register.Contracts.PartyUrn[] parties, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parties);

        if (parties.Length == 0)
        {
            return [];
        }

        try
        {
            using HttpRequestMessage requestMessage = new(HttpMethod.Post, _partiesQueryEndpoint)
            {
                Content = JsonContent.Create(new PartyQueryList<PartyUrn> { Data = [.. parties] }, options: _jsonSerializerOptions)
            };

            var accessToken = _accessTokenGenerator.GenerateAccessToken("platform", "notifications");
            if (!string.IsNullOrEmpty(accessToken))
            {
                requestMessage.Headers.Add("PlatformAccessToken", accessToken);
            }

            using var response = await _client.SendAsync(requestMessage, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw await PlatformHttpException.CreateAsync(response);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return [];
            }

            var result = await response.Content.ReadFromJsonAsync<PartyQueryList<Party>>(_jsonSerializerOptions, cancellationToken);
            return result?.Data ?? throw new JsonException("The register party query response must contain a data list.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw new PlatformDependencyException(nameof(RegisterClient), nameof(GetParties), ex);
        }
    }

    /// <inheritdoc/>
    /// <remarks>This method should be deprecated. Use <see cref="QueryPartyDetails"/> instead.</remarks>
    public async Task<List<PartyDetails>> GetPartyDetails(List<string>? organizationNumbers, List<string>? socialSecurityNumbers)
    {
        try
        {
            if ((organizationNumbers?.Count ?? 0) == 0 && (socialSecurityNumbers?.Count ?? 0) == 0)
            {
                return [];
            }

            using HttpRequestMessage requestMessage = new(HttpMethod.Post, _nameComponentsLookupEndpoint)
            {
                Content = JsonContent.Create(new PartyDetailsLookupBatch(organizationNumbers, socialSecurityNumbers), options: _jsonSerializerOptions)
            };

            var accessToken = _accessTokenGenerator.GenerateAccessToken("platform", "notifications");
            if (!string.IsNullOrEmpty(accessToken))
            {
                requestMessage.Headers.Add("PlatformAccessToken", accessToken);
            }

            var response = await _client.SendAsync(requestMessage, CancellationToken.None);

            if (!response.IsSuccessStatusCode)
            {
                throw await PlatformHttpException.CreateAsync(response);
            }

            var deserializeResponse = await response.Content.ReadFromJsonAsync<PartyDetailsLookupResult>(_jsonSerializerOptions);
            return deserializeResponse?.PartyDetailsList ?? [];
        }
        catch (Exception ex)
        {
            throw new PlatformDependencyException("RegisterClient", "GetPartyDetails", ex);
        }
    }

    /// <summary>
    /// Creates distinct party URNs from the given identifiers. Invalid identifiers are skipped since they cannot match a party.
    /// </summary>
    private static List<PartyUrn> CreatePartyUrns(IEnumerable<string> organizationNumbers, IEnumerable<string> nationalIdentityNumbers)
    {
        var urns = new List<PartyUrn>();

        foreach (var organizationNumber in organizationNumbers.Distinct())
        {
            if (OrganizationIdentifier.TryParse(organizationNumber, null, out var identifier))
            {
                urns.Add(PartyUrn.OrganizationId.Create(identifier));
            }
        }

        foreach (var nationalIdentityNumber in nationalIdentityNumbers.Distinct())
        {
            if (PersonIdentifier.TryParse(nationalIdentityNumber, null, out var identifier))
            {
                urns.Add(PartyUrn.PersonId.Create(identifier));
            }
        }

        return urns;
    }

    private static PartyDetails? ToPartyDetails(Party party)
    {
        var name = party.DisplayName.HasValue ? party.DisplayName.Value : null;

        return party switch
        {
            Person person => new PartyDetails { Name = name, NationalIdentityNumber = person.PersonIdentifier.ToString() },
            Organization organization => new PartyDetails { Name = name, OrganizationNumber = organization.OrganizationIdentifier.ToString() },
            _ => null
        };
    }
}
