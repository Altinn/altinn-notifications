using Altinn.Notifications.Functions.Configurations;
using Altinn.Notifications.Functions.Extensions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Altinn.Notifications.Functions.Integrations
{
    public class NotificationsClient : INotifications
    {
        private readonly HttpClient _client;
        private readonly IToken _token;
        private readonly ILogger _logger;

        public NotificationsClient(
            HttpClient client,
             IOptions<PlatformSettings> settings,
            ILogger<INotifications> logger)
        {
            _logger = logger;
            client.BaseAddress = new Uri(settings.Value.ApiNotificationsEndpoint);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _client = client;
        }

        public async Task<List<int>> GetOutboundEmails()
        {
            string path = "outbound/emails";
            string token = string.Empty; // await _token.GeneratePlatformToken();
            HttpResponseMessage res = await _client.GetAsync(path, token);

            List<int> outboundEmails = new();

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError($" // NotificationsClient // GetOutboundEmails // {res.StatusCode} - {await res.Content.ReadAsStringAsync()}");
                return outboundEmails;
            }

            var responseString = await res.Content.ReadAsStringAsync();
            outboundEmails.AddRange(JsonSerializer.Deserialize<List<int>>(responseString));
            return outboundEmails;
        }

        public async Task<List<int>> GetOutboundSMS()
        {
            string path = "outbound/sms";
            string token = string.Empty; // await _token.GeneratePlatformToken();
            HttpResponseMessage res = await _client.GetAsync(path, token);
            List<int> outboundSMS = new();

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("Something went terribly wrong!");
                return outboundSMS;
            }

            var responseString = await res.Content.ReadAsStringAsync();
            outboundSMS.AddRange(JsonSerializer.Deserialize<List<int>>(responseString));
            return outboundSMS;
        }


        public async Task TriggerSendTarget(string targetId)
        {
            _logger.LogInformation($"// NotificationsClient // Posting new targetId");
            string path = "/send";
            string token = string.Empty; // await _token.GeneratePlatformToken();
            HttpResponseMessage res = await _client.PostAsync(path, JsonContent.Create(targetId), token);

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("// NotificationsClient // Could not post target for sending!");
            }
        }
    }
}
