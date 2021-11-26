using Altinn.Notifications.Interfaces.Models;
using Altinn.Notifications.Tests.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Xunit;

namespace Altinn.Notifications.Tests
{
    public  class OutboundApiTests : IClassFixture<CustomWebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController>>
    {
        private readonly CustomWebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController> _factory;


        public OutboundApiTests(CustomWebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController> factory)
        {
             _factory = factory;
        }

        [Fact]
        public async Task Outbound_Email_GET_OK()
        {
            NotificationExt notificationeExt = new NotificationExt();
            HttpClient client = SetupUtil.GetTestClient(_factory);

            HttpRequestMessage reqst = new HttpRequestMessage(HttpMethod.Get, "notifications/api/v1/outbound/email")
            {
            };

            HttpResponseMessage response = await client.SendAsync(reqst);
            string responseContent = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            List<string> targets = System.Text.Json.JsonSerializer.Deserialize<List<string>>(responseContent, options);

            Assert.Equal(3, targets.Count);
        }
   
    }
}
