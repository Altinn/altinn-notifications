using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
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
    public EmailNotificationOrdersControllerTests(WebApplicationFactory<EmailNotificationOrdersController> factory)
    {
        _factory = factory;
        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
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
        var _validator = new Mock<IValidator<EmailNotificationOrderRequest>>();
        _validator.Setup(v => v.Validate(It.IsAny<EmailNotificationOrderRequest>()))
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
    public async Task Post_ValidationSuccessful_Successful()
    {
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);


        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }


    private HttpClient GetTestClient(IValidator<EmailNotificationOrderRequest>? validator = null)
    {
        if (validator == null)
        {
            var _validator = new Mock<IValidator<EmailNotificationOrderRequest>>();
            _validator.Setup(v => v.Validate(It.IsAny<EmailNotificationOrderRequest>()))
                .Returns(new ValidationResult());
            validator = _validator.Object;
        }


        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(validator);

                // Set up mock authentication so that not well known endpoint is used
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();

        return client;
    }
}
