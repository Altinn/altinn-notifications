using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Authorization.ProblemDetails;
using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Models.NotificationLog;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;
using Altinn.Notifications.Tests.Notifications.Utils;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.NotificationLogController;

public class NotificationLogControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.NotificationLogController>>
{
    private const string _basePath = "/notifications/api/v1/future/log";

    private static readonly Guid _smsNotificationId = Guid.NewGuid();
    private static readonly Guid _emailNotificationId = Guid.NewGuid();
    private static readonly DateTime _smsLastUpdateTime = DateTime.UtcNow.AddMinutes(-2);
    private static readonly DateTime _emailLastUpdateTime = DateTime.UtcNow.AddMinutes(-5);
    private static readonly DateTime _smsRequestedSendTime = DateTime.UtcNow.AddMinutes(-20);
    private static readonly DateTime _emailRequestedSendTime = DateTime.UtcNow.AddMinutes(-30);

    private readonly JsonSerializerOptions _options;
    private readonly Mock<INotificationLogService> _serviceMock;
    private readonly IntegrationTestWebApplicationFactory<Controllers.NotificationLogController> _factory;

    public NotificationLogControllerTests(IntegrationTestWebApplicationFactory<Controllers.NotificationLogController> factory)
    {
        _factory = factory;

        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        _serviceMock = new Mock<INotificationLogService>();
        _serviceMock
            .Setup(s => s.GetByDialogId(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableList.Create(CreateEmailSummary(), CreateSmsSummary()));
        _serviceMock
            .Setup(s => s.GetByTransmissionId(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableList.Create(CreateEmailSummary(), CreateSmsSummary()));
        _serviceMock
            .Setup(s => s.GetByDialogAndTransmissionIds(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableList.Create(CreateEmailSummary(), CreateSmsSummary()));
    }

    [Fact]
    public async Task Get_WithValidBearerTokenAndDialogId_ReturnsOkWithMatchingEntries()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage request = new(HttpMethod.Get, _basePath + "?dialogId=dialog-abc");

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var items = JsonSerializer.Deserialize<List<NotificationLogSummaryExt>>(content, _options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(items);
        Assert.Equal(2, items.Count);

        var smsEntry = Assert.Single(items, i => i.Channel == "Sms");
        Assert.Equal(_smsNotificationId, smsEntry.NotificationId);
        Assert.Equal("Sms", smsEntry.Channel);
        Assert.Equal("Reminder", smsEntry.Type);
        Assert.Equal("Delivered", smsEntry.Status);
        Assert.Equal("+4799999999", smsEntry.Destination);
        Assert.Equal(_smsLastUpdateTime, smsEntry.LastUpdateTime);
        Assert.Equal(_smsRequestedSendTime, smsEntry.RequestedSendTime);

        var emailEntry = Assert.Single(items, i => i.Channel == "Email");
        Assert.Equal("Email", emailEntry.Channel);
        Assert.Equal("Delivered", emailEntry.Status);
        Assert.Equal("Notification", emailEntry.Type);
        Assert.Equal("user@example.com", emailEntry.Destination);
        Assert.Equal(_emailLastUpdateTime, emailEntry.LastUpdateTime);
        Assert.Equal(_emailNotificationId, emailEntry.NotificationId);
        Assert.Equal(_emailRequestedSendTime, emailEntry.RequestedSendTime);

        _serviceMock.Verify(s => s.GetByDialogId("dialog-abc", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Get_WithValidBearerTokenAndTransmissionId_ReturnsOk()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage request = new(HttpMethod.Get, _basePath + "?transmissionId=transmission-xyz");

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var items = JsonSerializer.Deserialize<List<NotificationLogSummaryExt>>(content, _options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(items);
        Assert.Equal(2, items.Count);

        var smsEntry = Assert.Single(items, i => i.Channel == "Sms");
        Assert.Equal(_smsNotificationId, smsEntry.NotificationId);
        Assert.Equal("Sms", smsEntry.Channel);
        Assert.Equal("Reminder", smsEntry.Type);
        Assert.Equal("Delivered", smsEntry.Status);
        Assert.Equal("+4799999999", smsEntry.Destination);
        Assert.Equal(_smsLastUpdateTime, smsEntry.LastUpdateTime);
        Assert.Equal(_smsRequestedSendTime, smsEntry.RequestedSendTime);

        var emailEntry = Assert.Single(items, i => i.Channel == "Email");
        Assert.Equal("Email", emailEntry.Channel);
        Assert.Equal("Delivered", emailEntry.Status);
        Assert.Equal("Notification", emailEntry.Type);
        Assert.Equal("user@example.com", emailEntry.Destination);
        Assert.Equal(_emailLastUpdateTime, emailEntry.LastUpdateTime);
        Assert.Equal(_emailNotificationId, emailEntry.NotificationId);
        Assert.Equal(_emailRequestedSendTime, emailEntry.RequestedSendTime);

        _serviceMock.Verify(s => s.GetByTransmissionId("transmission-xyz", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Get_WithValidBearerTokenAndBothIdentifiers_ReturnsOk()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage request = new(HttpMethod.Get, _basePath + "?dialogId=dialog-abc&transmissionId=transmission-xyz");

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var items = JsonSerializer.Deserialize<List<NotificationLogSummaryExt>>(content, _options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(items);
        Assert.Equal(2, items.Count);

        var smsEntry = Assert.Single(items, i => i.Channel == "Sms");
        Assert.Equal(_smsNotificationId, smsEntry.NotificationId);
        Assert.Equal("Sms", smsEntry.Channel);
        Assert.Equal("Reminder", smsEntry.Type);
        Assert.Equal("Delivered", smsEntry.Status);
        Assert.Equal("+4799999999", smsEntry.Destination);
        Assert.Equal(_smsLastUpdateTime, smsEntry.LastUpdateTime);
        Assert.Equal(_smsRequestedSendTime, smsEntry.RequestedSendTime);

        var emailEntry = Assert.Single(items, i => i.Channel == "Email");
        Assert.Equal("Email", emailEntry.Channel);
        Assert.Equal("Delivered", emailEntry.Status);
        Assert.Equal("Notification", emailEntry.Type);
        Assert.Equal("user@example.com", emailEntry.Destination);
        Assert.Equal(_emailLastUpdateTime, emailEntry.LastUpdateTime);
        Assert.Equal(_emailNotificationId, emailEntry.NotificationId);
        Assert.Equal(_emailRequestedSendTime, emailEntry.RequestedSendTime);

        _serviceMock.Verify(s => s.GetByDialogAndTransmissionIds("dialog-abc", "transmission-xyz", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Get_WithValidPlatformAccessToken_ReturnsOk()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage request = new(HttpMethod.Get, _basePath + "?dialogId=dialog-abc");
        request.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var items = JsonSerializer.Deserialize<List<NotificationLogSummaryExt>>(content, _options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(items);
        Assert.Equal(2, items.Count);

        var smsEntry = Assert.Single(items, i => i.Channel == "Sms");
        Assert.Equal(_smsNotificationId, smsEntry.NotificationId);
        Assert.Equal("Sms", smsEntry.Channel);
        Assert.Equal("Reminder", smsEntry.Type);
        Assert.Equal("Delivered", smsEntry.Status);
        Assert.Equal("+4799999999", smsEntry.Destination);
        Assert.Equal(_smsLastUpdateTime, smsEntry.LastUpdateTime);
        Assert.Equal(_smsRequestedSendTime, smsEntry.RequestedSendTime);

        var emailEntry = Assert.Single(items, i => i.Channel == "Email");
        Assert.Equal("Email", emailEntry.Channel);
        Assert.Equal("Delivered", emailEntry.Status);
        Assert.Equal("Notification", emailEntry.Type);
        Assert.Equal("user@example.com", emailEntry.Destination);
        Assert.Equal(_emailLastUpdateTime, emailEntry.LastUpdateTime);
        Assert.Equal(_emailNotificationId, emailEntry.NotificationId);
        Assert.Equal(_emailRequestedSendTime, emailEntry.RequestedSendTime);

        _serviceMock.Verify(s => s.GetByDialogId("dialog-abc", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Get_WithoutBearerToken_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage request = new(HttpMethod.Get, _basePath + "?dialogId=dialog-abc");

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithUserToken_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetUserToken(1337));
        HttpRequestMessage request = new(HttpMethod.Get, _basePath + "?dialogId=dialog-abc");

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithInvalidScope_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "invalid:scope"));

        HttpRequestMessage request = new(HttpMethod.Get, _basePath + "?dialogId=dialog-abc");

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithNoQueryIdentifiersProvided_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage request = new(HttpMethod.Get, _basePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_WhenServiceThrowsOperationCanceledException_Returns499WithProblemDetails()
    {
        // Arrange
        var serviceMock = new Mock<INotificationLogService>();
        serviceMock
            .Setup(s => s.GetByDialogId(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        HttpClient client = GetTestClient(serviceMock.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage request = new(HttpMethod.Get, _basePath + "?dialogId=dialog-abc");

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problemDetails = JsonSerializer.Deserialize<AltinnProblemDetails>(content, _options);

        // Assert
        Assert.Equal((HttpStatusCode)499, response.StatusCode);

        Assert.NotNull(problemDetails);
        Assert.Equal("NOT-00002", problemDetails.ErrorCode.ToString());
        Assert.Equal((int)response.StatusCode, problemDetails.Status);
    }

    private HttpClient GetTestClient(INotificationLogService? service = null)
    {
        service ??= _serviceMock.Object;

        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(service);
                services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();

        return client;
    }

    private static NotificationLogSummary CreateEmailSummary() =>
        new()
        {
            Channel = "Email",
            Status = "Delivered",
            Type = "Notification",
            NotificationId = _emailNotificationId,
            Destination = "user@example.com",
            LastUpdateTime = _emailLastUpdateTime,
            RequestedSendTime = _emailRequestedSendTime
        };

    private static NotificationLogSummary CreateSmsSummary() =>
        new()
        {
            Channel = "Sms",
            Type = "Reminder",
            Status = "Delivered",
            Destination = "+4799999999",
            NotificationId = _smsNotificationId,
            LastUpdateTime = _smsLastUpdateTime,
            RequestedSendTime = _smsRequestedSendTime
        };
}
