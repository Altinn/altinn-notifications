using System.Net;

using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.TestingControllers;
public class TriggerControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.TriggerController>>
{
    private readonly IntegrationTestWebApplicationFactory<Controllers.TriggerController> _factory;
    private const string _basePath = "/notifications/api/v1/trigger";

    public TriggerControllerTests(IntegrationTestWebApplicationFactory<Controllers.TriggerController> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Trigger_PastDueOrders_OrderProcessingServiceCalled()
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
    public async Task Trigger_Trigger_SendEmailNotifications_EmailNotificationServiceCalled()
    {
        Mock<IEmailNotificationService> serviceMock = new();
        serviceMock
            .Setup(s => s.SendNotifications());

        var client = GetTestClient(null, serviceMock.Object);

        string url = _basePath + "/sendemail";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        serviceMock.VerifyAll();
    }

    private HttpClient GetTestClient(IOrderProcessingService? orderProcessingService = null, IEmailNotificationService? emailNotificationService = null)
    {
        if (orderProcessingService == null)
        {
            var _orderProcessingService = new Mock<IOrderProcessingService>();
            orderProcessingService = _orderProcessingService.Object;
        }
        if (emailNotificationService == null)
        {
            var _emailNotificationService = new Mock<IEmailNotificationService>();
            emailNotificationService = _emailNotificationService.Object;

        }

        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(orderProcessingService);
                services.AddSingleton(emailNotificationService);

                // Set up mock authentication so that not well known endpoint is used
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();

        return client;
    }
}