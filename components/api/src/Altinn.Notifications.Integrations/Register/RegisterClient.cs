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
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
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
        _client = client;
        _client.BaseAddress = new Uri(settings.Value.ApiRegisterEndpoint);

        _accessTokenGenerator = accessTokenGenerator;

        _jsonSerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    /// <inheritdoc/>
    public async Task<List<Party>> GetParties(IReadOnlyCollection<PartyUrn> parties, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parties);

        if (parties.Count == 0)
        {
            return [];
        }

        try
        {
            using HttpRequestMessage requestMessage = new(HttpMethod.Post, "notifications/parties/query")
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
            throw new PlatformDependencyException("RegisterClient", "GetParties", ex);
        }
    }

    /// <inheritdoc/>
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
}
