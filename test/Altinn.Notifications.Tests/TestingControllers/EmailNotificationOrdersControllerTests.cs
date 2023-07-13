using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Models;
using Altinn.Notifications.Tests.Mocks.Authentication;
using Altinn.Notifications.Tests.Utils;

using AltinnCore.Authentication.JwtCookie;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Moq;

using Xunit;

using ValidationResult = FluentValidation.Results.ValidationResult;

namespace Altinn.Notifications.Tests.TestingControllers;

public class EmailNotificationOrdersControllerTests : IClassFixture<WebApplicationFactory<EmailNotificationOrdersController>>
{
    private const string _basePath = "/notifications/api/v1/orders/email";

    private readonly WebApplicationFactory<EmailNotificationOrdersController> _factory;

    private readonly JsonSerializerOptions _options;

    private readonly EmailNotificationOrderRequestExt _orderRequestExt;
    private readonly NotificationOrder _order;

    public EmailNotificationOrdersControllerTests(WebApplicationFactory<EmailNotificationOrdersController> factory)
    {
        _factory = factory;
        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        _orderRequestExt = new()
        {
            Body = "email-body",
            ContentType = EmailContentType.Html,
            FromAddress = "sender@domain.com",
            Recipients = null,
            SendersReference = "senders-reference",
            SendTime = DateTime.UtcNow,
            Subject = "email-subject",            
            ToAddresses = new List<string>() { "recipient1@domain.com", "recipient2@domain.com" }
        };

        _order = new(
            Guid.NewGuid().ToString(),
            "senders-reference", 
            new List<INotificationTemplate>(),
            DateTime.UtcNow, 
            NotificationChannel.Email,
            new Creator("ttd"),
             DateTime.UtcNow,
            new List<Recipient>());

    }

    [Fact]
    public async Task Post_MissingBearerToken_Unauthorized()
    {
        //Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_EmptyBody_BadRequest()
    {
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

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
        var _validator = new Mock<IValidator<EmailNotificationOrderRequestExt>>();
        _validator.Setup(v => v.Validate(It.IsAny<EmailNotificationOrderRequestExt>()))
            .Returns(new ValidationResult(new List<ValidationFailure> { new ValidationFailure("SomeProperty", "SomeError") }));


        HttpClient client = GetTestClient(_validator.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

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
    public async Task Post_ServiceReturnsError_ServerError()
    {
        // Arrange
        Mock<IEmailNotificationOrderService> serviceMock = new();
        serviceMock.Setup(s => s.RegisterEmailNotificationOrder(It.IsAny<NotificationOrderRequest>()))
            .ReturnsAsync((null, new ServiceError(500)));



        HttpClient client = GetTestClient(orderService: serviceMock.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

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
    public async Task Post_ServiceReturnsOrder_Accepted()
    {
        // Arrange
        Mock<IEmailNotificationOrderService> serviceMock = new();
        serviceMock.Setup(s => s.RegisterEmailNotificationOrder(It.IsAny<NotificationOrderRequest>()))
            .ReturnsAsync((_order, null));

        HttpClient client = GetTestClient(orderService: serviceMock.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(_orderRequestExt.Serialize(), Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);


        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        serviceMock.VerifyAll();
    }


    private HttpClient GetTestClient(IValidator<EmailNotificationOrderRequestExt>? validator = null, IEmailNotificationOrderService? orderService = null)
    {
        if (validator == null)
        {
            var _validator = new Mock<IValidator<EmailNotificationOrderRequestExt>>();
            _validator.Setup(v => v.Validate(It.IsAny<EmailNotificationOrderRequestExt>()))
                .Returns(new ValidationResult());
            validator = _validator.Object;
        }

        if (orderService == null)
        {
            var _orderService = new Mock<IEmailNotificationOrderService>();
            orderService = _orderService.Object;
        }


        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(validator);
                services.AddSingleton(orderService);

                // Set up mock authentication so that not well known endpoint is used
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();

        return client;
    }
}
