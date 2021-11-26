using Altinn.Notifications.Interfaces.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

using Xunit;

namespace Altinn.Notifications.Tests
{
    public  class SendApiTests : IClassFixture<CustomWebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController>>
    {
        private readonly CustomWebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController> _factory;

        private readonly HttpClient _client;

        public SendApiTests(CustomWebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        public async Task Send_Post_Ok()
        {
            NotificationExt notificationeExt = new NotificationExt();

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
