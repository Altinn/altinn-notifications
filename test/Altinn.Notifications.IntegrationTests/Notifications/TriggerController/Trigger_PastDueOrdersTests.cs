using System.Net;

using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Tests.EndToEndTests;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.TriggerController;

public class Trigger_PastDueOrdersTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.TriggerController>>, IDisposable
{
    private const string _basePath = "/notifications/api/v1/trigger/pastdueorders";

    private readonly IntegrationTestWebApplicationFactory<Controllers.TriggerController> _factory;
    private readonly string _topicName = Guid.NewGuid().ToString();

    public Trigger_PastDueOrdersTests(IntegrationTestWebApplicationFactory<Controllers.TriggerController> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// When the past due orders endpoint is triggered, we expect all orders that are past due to be pushed to the
    /// past due orders kafka topic and that each orders gets the status 'Processsing' in the database.
    /// </summary>
    [Fact]
    public async Task Trigger_PastDueOrders_ResposeOk()
    {
        // Arrange
        string orderId = await TestdataUtil.PopulateDBWithOrder();
        string sql = $"select count(1) from notifications.orders where processedstatus = 'Processing' and alternateid='{orderId}'";

        HttpClient client = GetTestClient();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        int actual = await TestdataUtil.RunSqlReturnIntOutput(sql);
        
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
                    opts.TopicList = new List<string> { _topicName };
                });
                services.Configure<Altinn.Notifications.Core.Configuration.KafkaSettings>(opts =>
                {
                    opts.PastDueOrdersTopicName = _topicName;
                });

                // Set up mock authentication so that not well known endpoint is used
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();

            });
        }).CreateClient();

        return client;
    }
}