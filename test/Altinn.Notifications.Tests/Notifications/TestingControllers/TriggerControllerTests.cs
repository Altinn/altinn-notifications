using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingControllers;
public class TriggerControllerTests : IClassFixture<CustomWebApplicationFactory<TriggerController>>
{
    private readonly CustomWebApplicationFactory<TriggerController> _factory;
    private const string _basePath = "/notifications/api/v1/trigger";

    public TriggerControllerTests(CustomWebApplicationFactory<TriggerController> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Trigger_PastDueOrders_RightServiceTriggered()
    {
        Mock<IOrderProcessingService> serviceMock = new();
        serviceMock
            .Setup(s => s.StartProcessingPastDueOrders());

        var client = GetTestClient(serviceMock.Object);
    
        string url = _basePath + "/pastdueorders";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task Trigger_PendingOrders_RightServiceTriggered()
    {
        Mock<IOrderProcessingService> serviceMock = new();
        serviceMock
            .Setup(s => s.StartProcessPendingOrders());

        var client = GetTestClient(serviceMock.Object);

        string url = _basePath + "/pendingorders";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        serviceMock.VerifyAll();
    }

    private HttpClient GetTestClient(IOrderProcessingService? orderProcessingService = null)
    {
        if (orderProcessingService == null)
        {
            var _orderProcessingService = new Mock<IOrderProcessingService>();
            orderProcessingService = _orderProcessingService.Object;
        }

        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(orderProcessingService);

                // Set up mock authentication so that not well known endpoint is used
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();

        return client;
    }
}