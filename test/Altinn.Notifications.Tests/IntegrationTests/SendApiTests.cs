using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Tests.IntegrationTests.Utils;

using Xunit;

namespace Altinn.Notifications.Tests.IntegrationTests
{
    public  class SendApiTests : IClassFixture<CustomWebApplicationFactory<NotificationsController>>
    {
        private readonly HttpClient _client;

        public SendApiTests(CustomWebApplicationFactory<NotificationsController> factory)
        {
            _client = factory.CreateClient();
        }

        public async Task Send_Post_Ok()
        {
            HttpRequestMessage reqst = new HttpRequestMessage(HttpMethod.Post, "notifications/api/v1/send/")
            {
                Content = JsonContent.Create("1")
            };

            HttpResponseMessage response = await _client.SendAsync(reqst);
            string responseContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("sendt", responseContent);
        }
    }
}
