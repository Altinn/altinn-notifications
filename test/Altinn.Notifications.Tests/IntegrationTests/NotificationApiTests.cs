using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Interfaces.Models;
using Altinn.Notifications.Tests.IntegrationTests.Utils;

using Xunit;

namespace Altinn.Notifications.Tests.IntegrationTests
{
    public  class NotificationApiTests : IClassFixture<CustomWebApplicationFactory<NotificationsController>>
    {
        private readonly CustomWebApplicationFactory<NotificationsController> _factory;
 
        public NotificationApiTests(CustomWebApplicationFactory<NotificationsController> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Notification_Post_Ok()
        {
            NotificationExt notificationeExt = new NotificationExt();
            notificationeExt.InstanceId = "2934823947/234234324";
            notificationeExt.Messages = new List<MessageExt>();
            notificationeExt.Messages.Add(new MessageExt() { EmailBody = "Email body", EmailSubject = "Email Subject", Langauge = "en", SmsText = "SMS body" });
            notificationeExt.Targets = new List<TargetExt>();
            notificationeExt.Targets.Add(new TargetExt() { ChannelType = "Email", Address = "test@altinnunittest.no" });

            HttpClient client = SetupUtil.GetTestClient(_factory);

            HttpRequestMessage reqst = new HttpRequestMessage(HttpMethod.Post, "notifications/api/v1/notifications/")
            {
                Content = new StringContent(JsonSerializer.Serialize(notificationeExt), Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await client.SendAsync(reqst);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            string content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            NotificationExt? notificationExtResponse = JsonSerializer.Deserialize<NotificationExt>(content, options);
            Assert.Equal(notificationeExt.InstanceId, notificationExtResponse.InstanceId);
        }
    }
}
