using System.Net;

using Altinn.Notifications.Core.Enums;
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

/// <summary>
/// Integration tests for the TriggerController SMS notification endpoints.
/// Tests both daytime and anytime SMS sending, including schedule restrictions
/// and cancellation handling.
/// </summary>
public class Trigger_SendSmsNotificationsTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.TriggerController>>, IAsyncLifetime
{
    private const string _sendSmsDaytimePath = "/notifications/api/v1/trigger/sendsms";
    private const string _sendSmsAnytimePath = "/notifications/api/v1/trigger/sendsmsanytime";

    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";
    private readonly string _topicName = Guid.NewGuid().ToString();
    private readonly DateTime _currentTime = DateTime.UtcNow.Date.AddHours(10);
    private readonly IntegrationTestWebApplicationFactory<Controllers.TriggerController> _factory;

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
    /// Tests that SMS notifications are not processed when the daytime endpoint
    /// is called outside allowed business hours. The endpoint returns OK,
    /// but no notifications are processed.
    /// </summary>
    [Fact]
    public async Task SendSmsDaytime_OutsideAllowedHours_DoesNotProcess()
    {
        // Arrange
        (_, SmsNotification notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendersReference: _sendersRef);

        HttpClient client = GetTestClient(canSendSmsNow: false);
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _sendSmsDaytimePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        string sql = $"select count(1) from notifications.smsnotifications where result = 'Sending' and alternateid='{notification.Id}'";
        long actual = await PostgreUtil.RunSqlReturnOutput<long>(sql);

        Assert.Equal(0, actual);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Tests that SMS notifications with status 'New' are successfully processed
    /// when sent through the anytime endpoint, regardless of business hours.
    /// The notifications should be pushed to Kafka and updated to 'Sending' status.
    /// </summary>
    [Fact]
    public async Task SendSmsAnytime_ProcessesSuccessfully_RegardlessOfHours()
    {
        // Arrange
        (_, SmsNotification notification) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendersReference: _sendersRef, sendingTimePolicy: SendingTimePolicy.Anytime);

        HttpClient client = GetTestClient();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _sendSmsAnytimePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        await Task.Delay(20); // give some time for the background service to process the notification. Todo: find better way to await processing

        // Assert
        string sql = $"select count(1) from notifications.smsnotifications where result = 'Sending' and alternateid='{notification.Id}'";
        long actual = await PostgreUtil.RunSqlReturnOutput<long>(sql);

        Assert.Equal(1L, actual);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Creates a test client with appropriate configuration for testing SMS notification endpoints.
    /// </summary>
    /// <param name="canSendSmsNow">Whether the schedule service should allow SMS sending now</param>
    /// <returns>A configured HTTP client for making requests to the test server</returns>
    private HttpClient GetTestClient(bool canSendSmsNow = true)
    {
        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                // Set up temp topic
                services.Configure<KafkaSettings>(opts =>
                {
                    opts.Admin.TopicList = [_topicName];
                });

                services.Configure<Altinn.Notifications.Core.Configuration.KafkaSettings>(opts =>
                {
                    opts.SmsQueueTopicName = _topicName;
                });

                // Configure test time
                Mock<IDateTimeService> dateMock = new();
                dateMock.Setup(e => e.UtcNow()).Returns(_currentTime);
                services.AddSingleton(dateMock.Object);

                // Mock schedule service when needed
                if (!canSendSmsNow)
                {
                    Mock<INotificationScheduleService> scheduleMock = new();
                    scheduleMock.Setup(e => e.CanSendSmsNow()).Returns(false);
                    services.AddSingleton(scheduleMock.Object);
                }

                // Set up mock authentication and authorization               
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();

        return client;
    }
}
