using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.IntegrationTests;
using Altinn.Notifications.Models.Delivery;
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

public class ShipmentControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<ShipmentController>>
{
    private readonly JsonSerializerOptions _options;
    private readonly Guid _shipmentId = Guid.NewGuid();
    private readonly Mock<INotificationDeliveryManifestService> _serviceMock;
    private const string _basePath = "/notifications/api/v1/future/shipment";
    private readonly IntegrationTestWebApplicationFactory<ShipmentController> _factory;

    public ShipmentControllerTests(IntegrationTestWebApplicationFactory<ShipmentController> factory)
    {
        _factory = factory;

        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        _serviceMock = new Mock<INotificationDeliveryManifestService>();
        _serviceMock
            .Setup(s => s.GetDeliveryManifestAsync(
                It.Is<Guid>(g => g.Equals(_shipmentId)),
                It.Is<string>(s => s.Equals("ttd")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDeliveryManifest(_shipmentId));
    }

    [Fact]
    public async Task GetById_ValidBearerToken_ReturnsOk()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        string url = _basePath + "/" + _shipmentId;
        HttpRequestMessage request = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(request);
        string responseString = await response.Content.ReadAsStringAsync();
        NotificationDeliveryManifestExt? manifest = JsonSerializer.Deserialize<NotificationDeliveryManifestExt>(responseString, _options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(manifest);
        Assert.Null(manifest.SequenceNumber);
        Assert.Equal("Completed", manifest.Status);
        Assert.Equal("Notification", manifest.Type);
        Assert.Equal(2, manifest.Recipients.Count);
        Assert.Equal(_shipmentId, manifest.ShipmentId);
        Assert.Equal("COMPLETED-ORDER-REF-F10D5B2DCDFD", manifest.SendersReference);
        Assert.True((DateTime.UtcNow.AddDays(-7) - manifest.LastUpdate).TotalSeconds < 5);

        Assert.Equal(2, manifest.Recipients.Count);

        var smsRecipient = Assert.IsType<IDeliveryManifestExt>(manifest.Recipients[0], exactMatch: false);
        Assert.Equal("Delivered", smsRecipient.Status);
        Assert.Equal("+4799999999", smsRecipient.Destination);

        var emailRecipient = Assert.IsType<IDeliveryManifestExt>(manifest.Recipients[1], exactMatch: false);
        Assert.Equal("Sent", emailRecipient.Status);
        Assert.Equal("test@example.com", emailRecipient.Destination);

        _serviceMock.Verify(
            s => s.GetDeliveryManifestAsync(It.Is<Guid>(e => e.Equals(_shipmentId)), It.Is<string>(e => e.Equals("ttd")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetById_UserToken_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetUserToken(1337));
        string url = _basePath + "/" + _shipmentId;
        HttpRequestMessage request = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_InvalidScope_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "invalid:scope"));

        string url = _basePath + "/" + _shipmentId;
        HttpRequestMessage request = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_MissingCreator_ReturnsForbidden()
    {
        // Arrange
        var controller = new ShipmentController(_serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        // Act
        var result = await controller.GetById(Guid.NewGuid());

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
        _serviceMock.Verify(
            s => s.GetDeliveryManifestAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetById_MissingBearer_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = GetTestClient();
        string url = _basePath + "/" + _shipmentId;
        HttpRequestMessage request = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ValidPlatformAccessToken_ReturnsOk()
    {
        // Arrange
        HttpClient client = GetTestClient();
        string url = _basePath + "/" + _shipmentId;
        HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Add("PlatformAccessToken", PrincipalUtil.GetAccessToken("ttd", "apps-test"));

        // Act
        HttpResponseMessage response = await client.SendAsync(request);
        string responseString = await response.Content.ReadAsStringAsync();
        NotificationDeliveryManifestExt? manifest = JsonSerializer.Deserialize<NotificationDeliveryManifestExt>(responseString, _options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(manifest);
        Assert.Null(manifest.SequenceNumber);
        Assert.Equal("Completed", manifest.Status);
        Assert.Equal("Notification", manifest.Type);
        Assert.Equal(2, manifest.Recipients.Count);
        Assert.Equal(_shipmentId, manifest.ShipmentId);
        Assert.Equal("COMPLETED-ORDER-REF-F10D5B2DCDFD", manifest.SendersReference);
        Assert.True((DateTime.UtcNow.AddDays(-7) - manifest.LastUpdate).TotalSeconds < 5);

        Assert.Equal(2, manifest.Recipients.Count);

        var smsRecipient = Assert.IsType<IDeliveryManifestExt>(manifest.Recipients[0], exactMatch: false);
        Assert.Equal("Delivered", smsRecipient.Status);
        Assert.Equal("+4799999999", smsRecipient.Destination);

        var emailRecipient = Assert.IsType<IDeliveryManifestExt>(manifest.Recipients[1], exactMatch: false);
        Assert.Equal("Sent", emailRecipient.Status);
        Assert.Equal("test@example.com", emailRecipient.Destination);

        _serviceMock.Verify(
            s => s.GetDeliveryManifestAsync(It.Is<Guid>(e => e.Equals(_shipmentId)), It.Is<string>(e => e.Equals("ttd")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetById_EnsuresCancellationTokenIsPassedToService()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var serviceMock = new Mock<INotificationDeliveryManifestService>();
        serviceMock
            .Setup(s => s.GetDeliveryManifestAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                cancellationToken))
            .ReturnsAsync(CreateDeliveryManifest(_shipmentId));

        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new ShipmentController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        // Act
        await controller.GetById(_shipmentId, cancellationToken);

        // Assert
        serviceMock.Verify(
            s => s.GetDeliveryManifestAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task GetById_DirectlyInvokeController_ReturnsMappedManifest()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new ShipmentController(_serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        // Act
        var result = await controller.GetById(_shipmentId);

        // Assert
        var happyPathResult = Assert.IsType<OkObjectResult>(result.Result);
        var manifest = Assert.IsType<NotificationDeliveryManifestExt>(happyPathResult.Value);

        Assert.NotNull(manifest);
        Assert.Null(manifest.SequenceNumber);
        Assert.Equal("Completed", manifest.Status);
        Assert.Equal("Notification", manifest.Type);
        Assert.Equal(2, manifest.Recipients.Count);
        Assert.Equal(_shipmentId, manifest.ShipmentId);
        Assert.Equal("COMPLETED-ORDER-REF-F10D5B2DCDFD", manifest.SendersReference);
        Assert.True((DateTime.UtcNow.AddDays(-7) - manifest.LastUpdate).TotalSeconds < 5);

        Assert.Equal(2, manifest.Recipients.Count);

        var smsRecipient = Assert.IsType<IDeliveryManifestExt>(manifest.Recipients[0], exactMatch: false);
        Assert.Equal("Delivered", smsRecipient.Status);
        Assert.Equal("+4799999999", smsRecipient.Destination);

        var emailRecipient = Assert.IsType<IDeliveryManifestExt>(manifest.Recipients[1], exactMatch: false);
        Assert.Equal("Sent", emailRecipient.Status);
        Assert.Equal("test@example.com", emailRecipient.Destination);

        _serviceMock.Verify(
            s => s.GetDeliveryManifestAsync(It.Is<Guid>(e => e.Equals(_shipmentId)), It.Is<string>(e => e.Equals("ttd")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetById_ServiceReturnsNotFound_ReturnsNotFoundStatusCode()
    {
        // Arrange
        var serviceMock = new Mock<INotificationDeliveryManifestService>();
        serviceMock
            .Setup(s => s.GetDeliveryManifestAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceError(404, "Shipment not found"));

        HttpClient client = GetTestClient(serviceMock.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        string url = _basePath + "/" + Guid.NewGuid();
        HttpRequestMessage request = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_OperationCanceled_ReturnsClientClosedRequestStatusCode()
    {
        // Arrange
        var serviceMock = new Mock<INotificationDeliveryManifestService>();
        serviceMock
            .Setup(s => s.GetDeliveryManifestAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        HttpClient client = GetTestClient(serviceMock.Object);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        string url = _basePath + "/" + Guid.NewGuid();
        HttpRequestMessage request = new(HttpMethod.Get, url);

        // Act
        HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        Assert.Equal((HttpStatusCode)499, response.StatusCode); // 499 Client Closed Request
        string content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Request terminated", content);
    }

    private HttpClient GetTestClient(INotificationDeliveryManifestService? service = null)
    {
        service ??= _serviceMock.Object;

        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(service);
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
            });
        }).CreateClient();

        return client;
    }

    private static Result<INotificationDeliveryManifest, ServiceError> CreateDeliveryManifest(Guid shipmentId)
    {
        var recipients = new List<IDeliveryManifest>
        {
            CreateSmsDeliveryManifest("+4799999999", "Delivered", DateTime.Now.AddDays(-7)),
            CreateEmailDeliveryManifest("test@example.com", "Sent", DateTime.Now.AddDays(-20))
        };

        return new NotificationDeliveryManifest
        {
            Status = "Completed",
            Type = "Notification",
            SequenceNumber = null,
            ShipmentId = shipmentId,
            LastUpdate = DateTime.UtcNow.AddDays(-7),
            Recipients = recipients.ToImmutableList(),
            SendersReference = "COMPLETED-ORDER-REF-F10D5B2DCDFD"
        };
    }

    private static SmsDeliveryManifest CreateSmsDeliveryManifest(string phoneNumber, string status, DateTime lastUpdate)
    {
        return new SmsDeliveryManifest()
        {
            Status = status,
            LastUpdate = lastUpdate,
            Destination = phoneNumber
        };
    }

    private static EmailDeliveryManifest CreateEmailDeliveryManifest(string emailAddress, string status, DateTime lastUpdate)
    {
        return new EmailDeliveryManifest()
        {
            Status = status,
            LastUpdate = lastUpdate,
            Destination = emailAddress
        };
    }
}
