using System.Net;
using System.Net.Http.Headers;

using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;
using Altinn.Notifications.Tests.Notifications.Utils;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.TestingControllers;

public class OrdersControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.OrdersController>>
{
    private const string _basePath = "/notifications/api/v1/orders";

    private readonly IntegrationTestWebApplicationFactory<Controllers.OrdersController> _factory;
    private readonly NotificationOrder _order;
    private readonly NotificationOrderWithStatus _orderWithStatus;

    public OrdersControllerTests(IntegrationTestWebApplicationFactory<Controllers.OrdersController> factory)
    {
        _factory = factory;

        _order = NotificationOrder
           .GetBuilder()
           .SetId(Guid.NewGuid())
           .SetSendersReference("senders-reference")
           .SetRequestedSendTime(DateTime.UtcNow)
           .SetNotificationChannel(NotificationChannel.Email)
           .SetIgnoreReservation(false)
           .SetCreator(new Creator("ttd"))
           .SetCreated(DateTime.UtcNow)
           .SetTemplates([])
           .SetRecipients([])
           .Build();

        _orderWithStatus = NotificationOrderWithStatus
                            .GetBuilder()
                            .SetId(Guid.NewGuid())
                            .SetSendersReference("senders-reference")
                            .SetRequestedSendTime(DateTime.UtcNow)
                            .SetCreator("ttd")
                            .SetCreated(DateTime.UtcNow)
                            .SetNotificationChannel(NotificationChannel.Email)
                            .SetIgnoreReservation(false)
                            .SetProcessingStatus(new ProcessingStatus())
                            .Build();
    }

    [Fact]
    public async Task GetBySendersRef_MissingBearer_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = GetTestClient();
        string url = _basePath + "?sendersReference=" + "internal-ref";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBySendersRef_CalledByUser_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetUserToken(1337));

        string url = _basePath + "?sendersReference=" + "internal-ref";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBySendersRef_CalledWithInvalidScope_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "dummy:scope"));

        string url = _basePath + "?sendersReference=" + "internal-ref";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBySendersRef_ValidBearerToken_CorrespondingServiceMethodCalled()
    {
        // Arrange
        var orderService = new Mock<IGetOrderService>();
        orderService
             .Setup(o => o.GetOrdersBySendersReference(It.Is<string>(s => s.Equals("internal-ref")), It.Is<string>(s => s.Equals("ttd"))))
             .ReturnsAsync(new List<NotificationOrder>() { _order });

        HttpClient client = GetTestClient(orderService.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        string url = _basePath + "?sendersReference=" + "internal-ref";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        orderService.VerifyAll();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBySendersRef_ValidPlatformAccessToken_CorrespondingServiceMethodCalled()
    {
        // Arrange
        var orderService = new Mock<IGetOrderService>();
        orderService
             .Setup(o => o.GetOrdersBySendersReference(It.Is<string>(s => s.Equals("internal-ref")), It.Is<string>(s => s.Equals("ttd"))))
             .ReturnsAsync(new List<NotificationOrder>() { _order });

        HttpClient client = GetTestClient(orderService.Object);

        string url = _basePath + "?sendersReference=" + "internal-ref";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);
        httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        orderService.VerifyAll();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_MissingBearer_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = GetTestClient();
        string url = _basePath + "/" + Guid.NewGuid();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_CalledByUser_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetUserToken(1337));

        string url = _basePath + "/" + Guid.NewGuid();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_CalledWithInvalidScope_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "dummy:scope"));

        string url = _basePath + "/" + Guid.NewGuid();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ValidBearerToken_CorrespondingServiceMethodCalled()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();

        var orderService = new Mock<IGetOrderService>();
        orderService
             .Setup(o => o.GetOrderById(It.Is<Guid>(g => g.Equals(orderId)), It.Is<string>(s => s.Equals("ttd"))))
             .ReturnsAsync(_order);

        HttpClient client = GetTestClient(orderService.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        string url = _basePath + "/" + orderId;
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        orderService.VerifyAll();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ValidPlatformAccessToken_CorrespondingServiceMethodCalled()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();

        var orderService = new Mock<IGetOrderService>();
        orderService
             .Setup(o => o.GetOrderById(It.Is<Guid>(g => g.Equals(orderId)), It.Is<string>(s => s.Equals("ttd"))))
             .ReturnsAsync(_order);

        HttpClient client = GetTestClient(orderService.Object);

        string url = _basePath + "/" + orderId;
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);
        httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        orderService.VerifyAll();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ServiceReturnsError_StatusCodeMatchesError()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();

        var orderService = new Mock<IGetOrderService>();
        orderService
             .Setup(o => o.GetOrderById(It.Is<Guid>(g => g.Equals(orderId)), It.Is<string>(s => s.Equals("ttd"))))
             .ReturnsAsync(new ServiceError(404));

        HttpClient client = GetTestClient(orderService.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        string url = _basePath + "/" + orderId;
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        orderService.VerifyAll();
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWithStatusById_MissingBearer_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = GetTestClient();
        string url = _basePath + "/" + Guid.NewGuid() + "/status";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWithStatusById_CalledByUser_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetUserToken(1337));

        string url = _basePath + "/" + Guid.NewGuid() + "/status";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetWithStatusById_CalledWithInvalidScope_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "dummy:scope"));

        string url = _basePath + "/" + Guid.NewGuid() + "/status";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetWithStatusById_ValidBearerToken_CorrespondingServiceMethodCalled()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();

        var orderService = new Mock<IGetOrderService>();
        orderService
             .Setup(o => o.GetOrderWithStatuById(It.Is<Guid>(g => g.Equals(orderId)), It.Is<string>(s => s.Equals("ttd"))))
             .ReturnsAsync(_orderWithStatus);

        HttpClient client = GetTestClient(orderService.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        string url = _basePath + "/" + orderId + "/status";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        orderService.VerifyAll();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWithStatusById_ValidPlatformAccessToken_CorrespondingServiceMethodCalled()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();

        var orderService = new Mock<IGetOrderService>();
        orderService
             .Setup(o => o.GetOrderWithStatuById(It.Is<Guid>(g => g.Equals(orderId)), It.Is<string>(s => s.Equals("ttd"))))
             .ReturnsAsync(_orderWithStatus);

        HttpClient client = GetTestClient(orderService.Object);

        string url = _basePath + "/" + orderId + "/status";

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);
        httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        orderService.VerifyAll();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWithStatusById_ServiceReturnsError_StatusCodeMatchesError()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();

        var orderService = new Mock<IGetOrderService>();
        orderService
             .Setup(o => o.GetOrderWithStatuById(It.Is<Guid>(g => g.Equals(orderId)), It.Is<string>(s => s.Equals("ttd"))))
             .ReturnsAsync(new ServiceError(404));

        HttpClient client = GetTestClient(orderService.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        string url = _basePath + "/" + orderId + "/status";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        orderService.VerifyAll();
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient GetTestClient(IGetOrderService? orderService = null)
    {
        if (orderService == null)
        {
            var orderServiceMock = new Mock<IGetOrderService>();
            orderServiceMock
                .Setup(o => o.GetOrderById(It.IsAny<Guid>(), It.IsAny<string>()))
                .ReturnsAsync(_order);

            orderServiceMock
                 .Setup(o => o.GetOrdersBySendersReference(It.IsAny<string>(), It.IsAny<string>()))
                 .ReturnsAsync(new List<NotificationOrder>() { _order });

            orderService = orderServiceMock.Object;
        }

        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(orderService);

                // Set up mock authentication and authorization
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
            });
        }).CreateClient();

        return client;
    }
}
