using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Common.AccessTokenClient.Services;
using Altinn.Notifications.Core;
using Altinn.Platform.Profile.Models;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations
{
    /// <summary>
    /// An implementation of <see cref="IProfileRetriever"/> using an HttpClient to retrieve profile information
    /// from the Altinn II Profile component.
    /// </summary>
    public class ProfileClient : IProfileRetriever
    {
        private readonly HttpClient _httpClient;
        private readonly IAccessTokenGenerator _accessTokenGenerator;
        private readonly IUserTokenProvider _userTokenProvider;

        private readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public ProfileClient(
            HttpClient httpClient, 
            IOptions<PlatformSettings> platformSettings, 
            IAccessTokenGenerator accessTokenGenerator,
            IUserTokenProvider userTokenProvider)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(platformSettings.Value.ProfileEndpointAddress);
            _httpClient.DefaultRequestHeaders.Add(
                platformSettings.Value.ProfileSubscriptionKeyHeaderName, 
                platformSettings.Value.ProfileSubscriptionKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _accessTokenGenerator = accessTokenGenerator;
            _userTokenProvider = userTokenProvider;
        }

        /// <summary>
        /// Retrieves a user profile based on it's unique id.
        /// </summary>
        /// <param name="userId">The unique id of the profile to retrieve.</param>
        /// <param name="ct">The cancellation token to cancel operation.</param>
        /// <returns>The user profile if it exists.</returns>
        public async Task<UserProfile?> GetUserProfile(int userId, CancellationToken ct)
        {           
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"users/{userId}");
            request.Headers.Add(
                "PlatformAccessToken", _accessTokenGenerator.GenerateAccessToken("platform", "notification"));
            request.Headers.Add(
                "Authorization", "Bearer " + _userTokenProvider.GetUserToken());

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return await response.Content.ReadFromJsonAsync<UserProfile>(jsonSerializerOptions, ct);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            throw await UnhandledHttpResponseException.CreateAsync(response);
        }

        /// <summary>
        /// Retrieves the user profile for a specified person using a national identity number.
        /// </summary>
        /// <param name="nationalIdentityNumber">The national identity number of a person.</param>
        /// <param name="ct">The cancellation token to cancel operation.</param>
        /// <returns>The identified user profile if found.</returns>
        public Task<UserProfile?> GetUserProfile(string nationalIdentityNumber, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
