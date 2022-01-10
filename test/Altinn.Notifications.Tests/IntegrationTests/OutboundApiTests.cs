using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Tests.IntegrationTests.Utils;

using Xunit;

namespace Altinn.Notifications.Tests.IntegrationTests
{
    public  class OutboundApiTests : IClassFixture<CustomWebApplicationFactory<NotificationsController>>
    {
        private readonly CustomWebApplicationFactory<NotificationsController> _factory;

        public OutboundApiTests(CustomWebApplicationFactory<NotificationsController> factory)
        {
             _factory = factory;
        }

        [Fact]
        public async Task Outbound_Email_GET_OK()
        {
            HttpClient client = SetupUtil.GetTestClient(_factory);

            HttpRequestMessage reqst = new HttpRequestMessage(HttpMethod.Get, "notifications/api/v1/outbound/email");

            HttpResponseMessage response = await client.SendAsync(reqst);
            string responseContent = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            List<string> targets = JsonSerializer.Deserialize<List<string>>(responseContent, options)!;

            Assert.Equal(3, targets.Count);
        }   
    }
}
