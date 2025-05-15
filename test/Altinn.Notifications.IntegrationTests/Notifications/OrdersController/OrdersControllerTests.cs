using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Models;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;
using Altinn.Notifications.Tests.Notifications.Utils;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.Mvc;
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
    private readonly NotificationOrderRequestResponse _requestResponse;
    private readonly NotificationOrderRequestExt _orderRequest;
    private readonly JsonSerializerOptions _options;
    private readonly Guid _orderId = Guid.NewGuid();

    public OrdersControllerTests(IntegrationTestWebApplicationFactory<Controllers.OrdersController> factory)
    {
        _factory = factory;

        _order = new(
            _orderId,
            "senders-reference",
            [],
            DateTime.UtcNow,
            NotificationChannel.Email,
            new Creator("ttd"),
            DateTime.UtcNow,
            [],
            false,
            null,
            null,
            OrderTypes.Notification);

        _orderWithStatus = new(
            _orderId,
            "senders-reference",
            DateTime.UtcNow,
            new Creator("ttd"),
            DateTime.UtcNow,
            NotificationChannel.Email,
            null,
            null,
            null,
            new ProcessingStatus(),
            OrderTypes.Notification);

        _requestResponse = new()
        {
            OrderId = _orderId,
            RecipientLookup = new()
        };

        _orderRequest = new()
        {
            NotificationChannel = NotificationChannelExt.EmailPreferred,
            EmailTemplate = new EmailTemplateExt { Subject = "Test", Body = "Test Body" },
            SmsTemplate = new SmsTemplateExt { Body = "Test Body" },
            Recipients = [new RecipientExt { NationalIdentityNumber = "16069412345" }],
            RequestedSendTime = DateTime.UtcNow.AddDays(1)
        };

        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
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

    [Fact]
    public async Task Post_MissingBearer_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_CalledByUser_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetUserToken(1337));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_CalledWithInvalidScope_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "dummy:scope"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidBearerToken_CorrespondingServiceMethodCalled()
    {
        // Arrange
        var orderRequestService = new Mock<IOrderRequestService>();
        orderRequestService
             .Setup(o => o.RegisterNotificationOrder(It.IsAny<NotificationOrderRequest>()))
             .ReturnsAsync(_requestResponse);

        HttpClient client = GetTestClient(orderRequestService: orderRequestService.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(JsonSerializer.Serialize(_orderRequest), Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string respoonseString = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        NotificationOrderRequestResponseExt? orderIdObjectExt = JsonSerializer.Deserialize<NotificationOrderRequestResponseExt>(respoonseString, _options);
        Assert.Equal(_orderId, orderIdObjectExt!.OrderId);
        Assert.Equal("http://localhost:5090/notifications/api/v1/orders/" + _orderId, response.Headers?.Location?.ToString());

        orderRequestService.VerifyAll();
    }

    [Fact]
    public async Task Post_ValidPlatformAccessToken_CorrespondingServiceMethodCalled()
    {
        // Arrange
        var orderRequestService = new Mock<IOrderRequestService>();
        orderRequestService
             .Setup(o => o.RegisterNotificationOrder(It.IsAny<NotificationOrderRequest>()))
             .ReturnsAsync(_requestResponse);

        HttpClient client = GetTestClient(orderRequestService: orderRequestService.Object);

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(JsonSerializer.Serialize(_orderRequest), Encoding.UTF8, "application/json")
        };

        httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string respoonseString = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        NotificationOrderRequestResponseExt? orderIdObjectExt = JsonSerializer.Deserialize<NotificationOrderRequestResponseExt>(respoonseString, _options);
        Assert.Equal(_orderId, orderIdObjectExt!.OrderId);
        Assert.Equal("http://localhost:5090/notifications/api/v1/orders/" + _orderId, response.Headers?.Location?.ToString());

        orderRequestService.VerifyAll();
    }

    [Fact]
    public async Task Post_InvalidOrderRequest_BadRequest()
    {
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        var invalidRequest = _orderRequest;
        invalidRequest.NotificationChannel = null;

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(JsonSerializer.Serialize(invalidRequest), Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        string content = await response.Content.ReadAsStringAsync();
        ProblemDetails? actual = JsonSerializer.Deserialize<ProblemDetails>(content, _options);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("One or more validation errors occurred.", actual?.Title);
    }

    [Fact]
    public async Task CancelOrder_MissingBearer_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = GetTestClient();
        string url = _basePath + "/" + Guid.NewGuid() + "/cancel";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Put, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CancelOrder_CalledByUser_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetUserToken(1337));

        string url = _basePath + "/" + Guid.NewGuid() + "/cancel";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Put, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CancelOrder_CalledWithInvalidScope_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "dummy:scope"));

        string url = _basePath + "/" + Guid.NewGuid() + "/cancel";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Put, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CancelOrder_ValidBearerToken_CorrespondingServiceMethodCalled()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();

        Mock<ICancelOrderService> orderService = new();
        orderService
             .Setup(o => o.CancelOrder(It.Is<Guid>(g => g.Equals(orderId)), It.Is<string>(s => s.Equals("ttd"))))
             .ReturnsAsync(_orderWithStatus);

        HttpClient client = GetTestClient(cancelOrderService: orderService.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        string url = _basePath + "/" + orderId + "/cancel";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Put, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        orderService.VerifyAll();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CancelOrder_ValidPlatformAccessToken_CorrespondingServiceMethodCalled()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();

        Mock<ICancelOrderService> orderService = new();
        orderService
                  .Setup(o => o.CancelOrder(It.Is<Guid>(g => g.Equals(orderId)), It.Is<string>(s => s.Equals("ttd"))))
          .ReturnsAsync(_orderWithStatus);

        HttpClient client = GetTestClient(cancelOrderService: orderService.Object);

        string url = _basePath + "/" + orderId + "/cancel";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Put, url);
        httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        orderService.VerifyAll();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CancelOrder_ServiceReturnsOrderNotFound_ReturnsNotFound()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();

        Mock<ICancelOrderService> orderService = new();
        orderService
                  .Setup(o => o.CancelOrder(It.Is<Guid>(g => g.Equals(orderId)), It.Is<string>(s => s.Equals("ttd"))))
          .ReturnsAsync(CancellationError.OrderNotFound);

        HttpClient client = GetTestClient(cancelOrderService: orderService.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        string url = _basePath + "/" + orderId + "/cancel";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Put, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        orderService.VerifyAll();
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CancelOrder_ServiceReturnsCancellationProhibited_ReturnsConflict()
    {
        // Arrange
        Guid orderId = Guid.NewGuid();

        Mock<ICancelOrderService> orderService = new();
        orderService
          .Setup(o => o.CancelOrder(It.Is<Guid>(g => g.Equals(orderId)), It.IsAny<string>()))
          .ReturnsAsync(CancellationError.CancellationProhibited);

        HttpClient client = GetTestClient(cancelOrderService: orderService.Object);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        string url = _basePath + "/" + orderId + "/cancel";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Put, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        orderService.VerifyAll();
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private HttpClient GetTestClient(IGetOrderService? getOrderService = null, IOrderRequestService? orderRequestService = null, ICancelOrderService? cancelOrderService = null)
    {
        if (getOrderService == null)
        {
            var orderServiceMock = new Mock<IGetOrderService>();
            orderServiceMock
                .Setup(o => o.GetOrderById(It.IsAny<Guid>(), It.IsAny<string>()))
                .ReturnsAsync(_order);

            orderServiceMock
                 .Setup(o => o.GetOrdersBySendersReference(It.IsAny<string>(), It.IsAny<string>()))
                 .ReturnsAsync(new List<NotificationOrder>() { _order });

            getOrderService = orderServiceMock.Object;
        }

        if (orderRequestService == null)
        {
            var orderRequestServiceMock = new Mock<IOrderRequestService>();
            orderRequestServiceMock
                .Setup(o => o.RegisterNotificationOrder(It.IsAny<NotificationOrderRequest>()))
                .ReturnsAsync(new NotificationOrderRequestResponse());

            orderRequestService = orderRequestServiceMock.Object;
        }

        if (cancelOrderService == null)
        {
            Mock<ICancelOrderService> cancelOrderMock = new();
            cancelOrderMock
                .Setup(o => o.CancelOrder(It.IsAny<Guid>(), It.IsAny<string>()))
                .ReturnsAsync(_orderWithStatus);
            cancelOrderService = cancelOrderMock.Object;
        }

        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(getOrderService);
                services.AddSingleton(orderRequestService);
                services.AddSingleton(cancelOrderService);

                // Set up mock authentication and authorization
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
            });
        }).CreateClient();

        return client;
    }
}
