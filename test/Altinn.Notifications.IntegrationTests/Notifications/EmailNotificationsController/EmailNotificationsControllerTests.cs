using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Services.Interfaces;
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

namespace Altinn.Notifications.IntegrationTests.Notifications.EmailNotificationsController;

public class EmailNotificationsControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.EmailNotificationsController>>
{
    private readonly string _basePath;
    private readonly string _invalidGuidBase;
    private readonly IntegrationTestWebApplicationFactory<Controllers.EmailNotificationsController> _factory;

    private readonly JsonSerializerOptions _options;

    public EmailNotificationsControllerTests(IntegrationTestWebApplicationFactory<Controllers.EmailNotificationsController> factory)
    {
        _basePath = $"/notifications/api/v1/orders/{Guid.NewGuid()}/notifications/email";
        _invalidGuidBase = "/notifications/api/v1/orders/1337;1=1/notifications/email";
        _factory = factory;
        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    [Fact]
    public async Task Get_MissingBearerToken_Unauthorized()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, _basePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_InvalidScopeInToken_Forbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:dummmy.scope"));
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, _basePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_InvalidGuid_BadRequest()
    {
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, _invalidGuidBase)
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
    public async Task Get_ServiceReturnsError_ServerError()
    {
        // Arrange
        Mock<INotificationSummaryService> serviceMock = new();
        serviceMock.Setup(s => s.GetEmailSummary(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync((null, new ServiceError(500)));

        HttpClient client = GetTestClient(summaryService: serviceMock.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, _basePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task Get_ValidScope_ServiceReturnsNotifications_Ok()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        EmailNotificationSummary output = new(id)
        {
            SendersReference = "senders-ref",
            Generated = 1,
            Succeeded = 1,
            Notifications = new List<EmailNotificationWithResult>()
        };

        Mock<INotificationSummaryService> serviceMock = new();
        serviceMock.Setup(s => s.GetEmailSummary(It.IsAny<Guid>(), It.Is<string>(s => s.Equals("ttd"))))
            .ReturnsAsync((output, null));

        HttpClient client = GetTestClient(summaryService: serviceMock.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, _basePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string respoonseString = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        EmailNotificationSummaryExt? summaryExt = JsonSerializer.Deserialize<EmailNotificationSummaryExt>(respoonseString);
        Assert.NotNull(summaryExt);
        Assert.Equal(id, summaryExt.OrderId);

        serviceMock.VerifyAll();
    }

    [Fact]
    public async Task Get_ValidAccessToken_ServiceReturnsOrder_Accepted()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        EmailNotificationSummary output = new(id)
        {
            SendersReference = "senders-ref",
            Generated = 1,
            Succeeded = 1,
            Notifications = new List<EmailNotificationWithResult>()
        };

        Mock<INotificationSummaryService> serviceMock = new();
        serviceMock.Setup(s => s.GetEmailSummary(It.IsAny<Guid>(), It.Is<string>(s => s.Equals("ttd"))))
            .ReturnsAsync((output, null));

        HttpClient client = GetTestClient(summaryService: serviceMock.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, _basePath);
        httpRequestMessage.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string respoonseString = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        EmailNotificationSummaryExt? summaryExt = JsonSerializer.Deserialize<EmailNotificationSummaryExt>(respoonseString);
        Assert.NotNull(summaryExt);
        Assert.Equal(id, summaryExt.OrderId);

        serviceMock.VerifyAll();
    }

    private HttpClient GetTestClient(INotificationSummaryService? summaryService = null)
    {
        if (summaryService == null)
        {
            var summaryServiceMock = new Mock<INotificationSummaryService>();
            summaryService = summaryServiceMock.Object;
        }

        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(summaryService);

                // Set up mock authentication and authorization
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
            });
        }).CreateClient();

        return client;
    }
}
