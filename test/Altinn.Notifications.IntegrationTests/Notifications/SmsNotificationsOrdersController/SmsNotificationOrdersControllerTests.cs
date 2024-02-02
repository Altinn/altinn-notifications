using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Models;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;
using Altinn.Notifications.Tests.Notifications.Utils;

using AltinnCore.Authentication.JwtCookie;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Moq;

using Xunit;

using ValidationResult = FluentValidation.Results.ValidationResult;

namespace Altinn.Notifications.IntegrationTests.Notifications.SmsNotificationsOrdersController;

public class SmsNotificationOrdersControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.SmsNotificationOrdersController>>
{
    private const string _basePath = "/notifications/api/v1/orders/sms";

    private readonly IntegrationTestWebApplicationFactory<Controllers.SmsNotificationOrdersController> _factory;

    private readonly JsonSerializerOptions _options;

    private readonly SmsNotificationOrderRequestExt _orderRequestExt;
    private readonly NotificationOrder _order;

    public SmsNotificationOrdersControllerTests(IntegrationTestWebApplicationFactory<Controllers.SmsNotificationOrdersController> factory)
    {
        _factory = factory;
        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        _orderRequestExt = new()
        {
            Body = "sms-body",
            Recipients = new List<RecipientExt>() { new RecipientExt() { MobileNumber = "+4740000001" }, new RecipientExt() { MobileNumber = "+4790000001" } },
            SendersReference = "senders-reference",
            RequestedSendTime = DateTime.UtcNow
        };

        _order = new(
            Guid.NewGuid(),
            "senders-reference",
            new List<INotificationTemplate>(),
            DateTime.UtcNow,
            NotificationChannel.Sms,
            new Creator("ttd"),
            DateTime.UtcNow,
            new List<Recipient>());
    }

    [Fact]
    public async Task Post_MissingBearerToken_Unauthorized()
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
    public async Task Post_InvalidScopeInToken_Forbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:dummmy.scope"));
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_EmptyBody_BadRequest()
    {
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
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
    public async Task Post_ValidationReturnsError_BadRequest()
    {
        var validator = new Mock<IValidator<SmsNotificationOrderRequestExt>>();
        validator.Setup(v => v.Validate(It.IsAny<SmsNotificationOrderRequestExt>()))
            .Returns(new ValidationResult(new List<ValidationFailure> { new ValidationFailure("SomeProperty", "SomeError") }));

        HttpClient client = GetTestClient(validator.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
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
    public async Task Post_UserClaimsPrincipal_Forbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetUserToken(1337));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(_orderRequestExt.Serialize(), Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_ServiceReturnsError_ServerError()
    {
        // Arrange
        Mock<IOrderRequestService> serviceMock = new();
        serviceMock.Setup(s => s.RegisterNotificationOrder(It.IsAny<NotificationOrderRequest>()))
            .ReturnsAsync(new ServiceError(500));

        HttpClient client = GetTestClient(orderService: serviceMock.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(_orderRequestExt.Serialize(), Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task Post_ValidScope_ServiceReturnsOrder_Accepted()
    {
        // Arrange
        Mock<IOrderRequestService> serviceMock = new();
        serviceMock.Setup(s => s.RegisterNotificationOrder(It.IsAny<NotificationOrderRequest>()))
              .Callback<NotificationOrderRequest>(orderRequest =>
              {
                  var smsTemplate = orderRequest.Templates
                      .OfType<SmsTemplate>()
                      .FirstOrDefault();

                  Assert.NotNull(smsTemplate);
                  Assert.Equal(string.Empty, smsTemplate.SenderNumber);
              })
            .ReturnsAsync(_order);

        HttpClient client = GetTestClient(orderService: serviceMock.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(_orderRequestExt.Serialize(), Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string respoonseString = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        OrderIdExt? orderIdObjectExt = JsonSerializer.Deserialize<OrderIdExt>(respoonseString);
        Assert.NotNull(orderIdObjectExt);
        Assert.Equal(_order.Id, orderIdObjectExt.OrderId);
        Assert.Equal("http://localhost:5090/notifications/api/v1/orders/" + _order.Id, response.Headers?.Location?.ToString());

        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task Post_ValidAccessToken_ServiceReturnsOrder_Accepted()
    {
        // Arrange
        Mock<IOrderRequestService> serviceMock = new();
        serviceMock.Setup(s => s.RegisterNotificationOrder(It.IsAny<NotificationOrderRequest>()))
              .Callback<NotificationOrderRequest>(orderRequest =>
              {
                  var smsTemplate = orderRequest.Templates
                      .OfType<SmsTemplate>()
                      .FirstOrDefault();

                  Assert.NotNull(smsTemplate);
                  Assert.Empty(smsTemplate.SenderNumber);
              })
            .ReturnsAsync(_order);

        HttpClient client = GetTestClient(orderService: serviceMock.Object);

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(_orderRequestExt.Serialize(), Encoding.UTF8, "application/json")
        };
        httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string respoonseString = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        OrderIdExt? orderIdObjectExt = JsonSerializer.Deserialize<OrderIdExt>(respoonseString);
        Assert.NotNull(orderIdObjectExt);
        Assert.Equal(_order.Id, orderIdObjectExt.OrderId);
        Assert.Equal("http://localhost:5090/notifications/api/v1/orders/" + _order.Id, response.Headers?.Location?.ToString());

        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task Post_OrderWithoutFromAddress_StringEmptyUsedAsServiceInput_Accepted()
    {
        // Arrange
        Mock<IOrderRequestService> serviceMock = new();

        serviceMock.Setup(s => s.RegisterNotificationOrder(It.IsAny<NotificationOrderRequest>()))
            .Callback<NotificationOrderRequest>(orderRequest =>
            {
                var smsTemplate = orderRequest.Templates
                    .OfType<SmsTemplate>()
                    .FirstOrDefault();

                Assert.NotNull(smsTemplate);
                Assert.Empty(smsTemplate.SenderNumber);
            })
            .ReturnsAsync(_order);

        HttpClient client = GetTestClient(orderService: serviceMock.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        SmsNotificationOrderRequestExt request = new()
        {
            Body = "sms-body",
            Recipients = new List<RecipientExt>() { new RecipientExt() { MobileNumber = "+4740000001" }, new RecipientExt() { MobileNumber = "+4790000001" } },
            SendersReference = "senders-reference",
            RequestedSendTime = DateTime.UtcNow
        };

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(request.Serialize(), Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string respoonseString = await response.Content.ReadAsStringAsync();
        OrderIdExt? orderIdObjectExt = JsonSerializer.Deserialize<OrderIdExt>(respoonseString);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(orderIdObjectExt);
        Assert.Equal(_order.Id, orderIdObjectExt.OrderId);

        Assert.Equal("http://localhost:5090/notifications/api/v1/orders/" + _order.Id, response.Headers?.Location?.ToString());

        serviceMock.VerifyAll();
    }

    private HttpClient GetTestClient(IValidator<SmsNotificationOrderRequestExt>? validator = null, IOrderRequestService? orderService = null)
    {
        if (validator == null)
        {
            var validatorMock = new Mock<IValidator<SmsNotificationOrderRequestExt>>();
            validatorMock.Setup(v => v.Validate(It.IsAny<SmsNotificationOrderRequestExt>()))
                .Returns(new ValidationResult());
            validator = validatorMock.Object;
        }

        if (orderService == null)
        {
            var orderServiceMock = new Mock<IOrderRequestService>();
            orderService = orderServiceMock.Object;
        }

        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                services.Configure<GeneralSettings>(opts =>
                {
                    opts.BaseUri = "http://localhost:5090";
                });

                services.AddSingleton(validator);
                services.AddSingleton(orderService);

                // Set up mock authentication and authorization
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
            });
        }).CreateClient();

        return client;
    }
}
