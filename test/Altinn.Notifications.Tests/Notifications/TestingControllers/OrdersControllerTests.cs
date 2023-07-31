using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;
using Altinn.Notifications.Tests.Notifications.Utils;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingControllers;
public class OrdersControllerTests : IClassFixture<CustomWebApplicationFactory<OrdersController>>
{
    private const string _basePath = "/notifications/api/v1/orders";

    private readonly CustomWebApplicationFactory<OrdersController> _factory;
    private readonly NotificationOrder _order;


    public OrdersControllerTests(CustomWebApplicationFactory<OrdersController> factory)
    {
        _factory = factory;

        _order = new(
            Guid.NewGuid(),
            "senders-reference",
            new List<INotificationTemplate>(),
            DateTime.UtcNow,
            NotificationChannel.Email,
            new Creator("ttd"),
             DateTime.UtcNow,
            new List<Recipient>());
    }

    [Fact]
    public async Task GetBySendersRef_MissingBearer_ReturnsUnauthorized()
    {
        //Arrange
        HttpClient client = GetTestClient();
        string url = _basePath + "?sendersReference=" + "internal-ref";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_MissingBearer_ReturnsUnauthorized()
    {
        //Arrange
        HttpClient client = GetTestClient();
        string url = _basePath + "/" + Guid.NewGuid();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBySendersRef_CorrespondingServiceMethodCalled()
    {
        //Arrange
        var orderService = new Mock<IOrderService>();
        orderService
             .Setup(o => o.GetOrderBySendersReference(It.Is<string>(s => s.Equals("internal-ref"))))
             .ReturnsAsync((_order, null));

        HttpClient client = GetTestClient(orderService.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

        string url = _basePath + "?sendersReference=" + "internal-ref";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        orderService.VerifyAll();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_CorrespondingServiceMethodCalled()
    {
        //Arrange
        Guid orderId = Guid.NewGuid();

        var orderService = new Mock<IOrderService>();
        orderService
             .Setup(o => o.GetOrderById(It.Is<Guid>(g => g.Equals(orderId))))
             .ReturnsAsync((_order, null));

        HttpClient client = GetTestClient(orderService.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

        string url = _basePath + "/" + orderId;
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        orderService.VerifyAll();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void HandleServiceResult_RequestorDoesNotMatchCreator_ReturnsForbidden()
    {
        // Arrange
        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(c => c.User).Returns(PrincipalUtil.GetClaimsPrincipal("nav", "987485654"));

        var controller = new OrdersController(null);
        controller.ControllerContext = new();
        controller.ControllerContext.HttpContext = httpContextMock.Object;

        // Act
        var res = controller.HandleServiceResult(_order, null);

        // Assert
        Assert.IsType<ForbidResult>(res.Result);
    }

    [Fact]
    public void HandleServiceResult_ServicerReturnsError_StatusCodeMatchesError()
    {
        // Arrange
        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(c => c.User).Returns(PrincipalUtil.GetClaimsPrincipal("ttd", "987485654"));

        var controller = new OrdersController(null);
        controller.ControllerContext = new();
        controller.ControllerContext.HttpContext = httpContextMock.Object;

        // Act
        var res = controller.HandleServiceResult(null, new ServiceError(404));

        // Assert
        Assert.IsType<ObjectResult>(res.Result);
        Assert.Equal(404, (res.Result as ObjectResult)!.StatusCode);
    }

    private HttpClient GetTestClient(IOrderService? orderService = null)
    {
        if (orderService == null)
        {
            var _orderService = new Mock<IOrderService>();
            _orderService
                .Setup(o => o.GetOrderById(It.IsAny<Guid>()))
                .ReturnsAsync((_order, null));

            _orderService
                 .Setup(o => o.GetOrderBySendersReference(It.IsAny<string>()))
                 .ReturnsAsync((_order, null));

            orderService = _orderService.Object;
        }

        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(orderService);

                // Set up mock authentication so that not well known endpoint is used
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();

        return client;
    }
}
