using System.Text;
using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.ContactPoints;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations.Register
{
    /// <summary>
    /// Implementation of the <see cref="IRegisterClient"/>
    /// </summary>
    public class RegisterClient : IRegisterClient
    {
        private readonly HttpClient _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegisterClient"/> class.
        /// </summary>
        public RegisterClient(HttpClient client, IOptions<PlatformSettings> settings)
        {
            _client = client;
            _client.BaseAddress = new Uri(settings.Value.ApiRegisterEndpoint);
        }

        /// <inheritdoc/>
        public async Task<List<OrganizationContactPoints>> GeOrganizationContactPoints(List<string> organizationNumbers)
        {
            var lookupObject = new OrgContactPointLookup
            {
                OrganizationNumbers = organizationNumbers
            };

            HttpContent content = new StringContent(JsonSerializer.Serialize(lookupObject, JsonSerializerOptionsProvider.Options), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync("organizations/contactpoint/lookup", content);

            if (!response.IsSuccessStatusCode)
            {
                throw new PlatformHttpException(response, $"RegisterClient.GetUnitContactPoints failed with status code {response.StatusCode}");
            }

            string responseContent = await response.Content.ReadAsStringAsync();
            List<OrganizationContactPoints>? contactPoints = JsonSerializer.Deserialize<OrgContactPointsList>(responseContent, JsonSerializerOptionsProvider.Options)!.ContactPointsList;
            return contactPoints!;
        }
    }
}
