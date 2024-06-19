using System.Net;

using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.TriggerController;

public class Trigger_SendSmsNotificationsTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.TriggerController>>, IAsyncLifetime
{
    private const string _basePath = "/notifications/api/v1/trigger/sendsms";

    private readonly IntegrationTestWebApplicationFactory<Controllers.TriggerController> _factory;
    private readonly string _topicName = Guid.NewGuid().ToString();
    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";
    private readonly DateTime currentTime = DateTime.UtcNow.Date.AddHours(10);

    public Trigger_SendSmsNotificationsTests(IntegrationTestWebApplicationFactory<Controllers.TriggerController> factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await PostgreUtil.DeleteOrderFromDb(_sendersRef);
        await KafkaUtil.DeleteTopicAsync(_topicName);
    }

    /// <summary>
    /// When the send sms endpoint is triggered, we expect all sms notifications with status 'New' to be pushed to the
    /// send sms kafka topic and that each notification gets the result status 'Sending' in the database.
    /// </summary>
    [Fact]
    public async Task Post_Ok()
    {
        // Arrange
        (_, SmsNotification notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendersReference: _sendersRef);

        HttpClient client = GetTestClient();

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        string sql = $"select count(1) from notifications.smsnotifications where result = 'Sending' and alternateid='{notification.Id}'";
        long actual = await PostgreUtil.RunSqlReturnOutput<long>(sql);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, actual);
    }

    private HttpClient GetTestClient()
    {
        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                // set up temp topic
                services.Configure<KafkaSettings>(opts =>
                {
                    opts.Admin.TopicList = new List<string> { _topicName };
                });
                services.Configure<Altinn.Notifications.Core.Configuration.KafkaSettings>(opts =>
                {
                    opts.SmsQueueTopicName = _topicName;
                });

                Mock<IDateTimeService> dateMock = new();
                dateMock.Setup(d => d.UtcNow()).Returns(currentTime);

                services.AddSingleton(dateMock.Object);
                // Set up mock authentication and authorization               
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();

        return client;
    }
}
