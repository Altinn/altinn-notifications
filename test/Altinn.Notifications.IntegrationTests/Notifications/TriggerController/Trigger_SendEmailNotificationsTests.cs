using System.Net;

using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.TriggerController;

public class Trigger_SendEmailNotificationsTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.TriggerController>>, IDisposable
{
    private const string _basePath = "/notifications/api/v1/trigger/sendemail";

    private readonly IntegrationTestWebApplicationFactory<Controllers.TriggerController> _factory;
    private readonly string _topicName = Guid.NewGuid().ToString();
    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";

    public Trigger_SendEmailNotificationsTests(IntegrationTestWebApplicationFactory<Controllers.TriggerController> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// When the send email endpoint is triggered, we expect all email notifications with status 'New' to be pushed to the
    /// send email kafka topic and that each notification gets the result status 'Sending' in the database.
    /// </summary>
    [Fact]
    public async Task Post_Ok()
    {
        // Arrange
        (_, EmailNotification notification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersReference: _sendersRef);

        HttpClient client = GetTestClient();

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        string sql = $"select count(1) from notifications.emailnotifications where result = 'Sending' and alternateid='{notification.Id}'";
        int actual = await PostgreUtil.RunSqlReturnIntOutput(sql);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, actual);
    }

    public async void Dispose()
    {
        await Dispose(true);

        GC.SuppressFinalize(this);
    }

    protected virtual async Task Dispose(bool disposing)
    {
        await PostgreUtil.DeleteOrderFromDb(_sendersRef);
        await KafkaUtil.DeleteTopicAsync(_topicName);
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
                    opts.PastDueOrdersTopicName = _topicName;
                });

                // Set up mock authentication and authorization               
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();

        return client;
    }
}
