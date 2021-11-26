using Altinn.Notifications.Core;
using Altinn.Notifications.Interfaces.Models;
using Altinn.Notifications.Tests.Mocks;
using Altinn.Notifications.Tests.Utils;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Xunit;

namespace Altinn.Notifications.Tests
{
    public  class NotificationApiTests : IClassFixture<CustomWebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController>>
    {
        private readonly CustomWebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController> _factory;

 
        public NotificationApiTests(CustomWebApplicationFactory<Altinn.Notifications.Controllers.NotificationsController> factory)
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
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(notificationeExt), Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await client.SendAsync(reqst);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            string content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            NotificationExt? notificationExtResponse = System.Text.Json.JsonSerializer.Deserialize<NotificationExt>(content, options);
            Assert.Equal(notificationeExt.InstanceId, notificationExtResponse.InstanceId);
        }
    }
}
