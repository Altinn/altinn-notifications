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
    public  class NotificationApiTests : IClassFixture<CustomWebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController>>
    {
        private readonly CustomWebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController> _factory;

        private readonly HttpClient _client;

        public NotificationApiTests(CustomWebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController> factory)
        {
           
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public void InitialTest()
        {
            string actual = "Stephanie er kul";

            Assert.Equal("Stephanie er kul", actual);
        }

        [Fact]
        public async Task Notification_Post_Ok()
        {
            NotificationExt notificationeExt = new NotificationExt();

            HttpRequestMessage reqst = new HttpRequestMessage(HttpMethod.Post, "api/notifications/")
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(notificationeExt), Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await _client.SendAsync(reqst);
            string responseContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("Hurra", responseContent);
        }
    }
}
