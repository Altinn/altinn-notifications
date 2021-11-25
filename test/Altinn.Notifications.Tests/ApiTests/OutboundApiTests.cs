using Altinn.Notifications.Interfaces.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Xunit;

namespace Altinn.Notifications.Tests
{
    public  class OutboundApiTests : IClassFixture<CustomWebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController>>
    {
        private readonly CustomWebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController> _factory;

        private readonly HttpClient _client;

        public OutboundApiTests(CustomWebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController> factory)
        {
             _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Outbound_Email_GET_OK()
        {
            NotificationExt notificationeExt = new NotificationExt();

            HttpRequestMessage reqst = new HttpRequestMessage(HttpMethod.Get, "notifications/api/v1/outbound/email")
            {
            };

            HttpResponseMessage response = await _client.SendAsync(reqst);
            string responseContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("1", responseContent);
        }

        [Fact]
        public async Task Outbound_SMS_GET_OK()
        {
            NotificationExt notificationeExt = new NotificationExt();

            HttpRequestMessage reqst = new HttpRequestMessage(HttpMethod.Get, "notifications/api/v1/outbound/sms")
            {
            };

            HttpResponseMessage response = await _client.SendAsync(reqst);
            string responseContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("1", responseContent);
        }
    }
}
