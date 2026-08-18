using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Authorization.ProblemDetails;
using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Dashboard;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Models.Dashboard;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;
using Altinn.Notifications.Tests.Notifications.Utils;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.DashboardController;

public class DashboardControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.DashboardController>>
{
    private const string _basePath = "/notifications/api/v1/future/dashboard";
    private const string _validScope = "altinn:notifications.support.admin";

    private const string _validNin = "16069412345";
    private const string _validOrgNumber = "123456789";
    private const string _validEmail = "recipient@example.com";
    private const string _validPhoneNumber = "+4799999999";

    private readonly JsonSerializerOptions _options;
    private readonly Mock<IDashboardService> _serviceMock;
    private readonly IntegrationTestWebApplicationFactory<Controllers.DashboardController> _factory;

    public DashboardControllerTests(IntegrationTestWebApplicationFactory<Controllers.DashboardController> factory)
    {
        _factory = factory;

        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        _serviceMock = new Mock<IDashboardService>();
    }

    // GET recipients/notifications/nin
    [Fact]
    public async Task GetByNin_ValidRequest_ReturnsOkWithExpectedPayload()
    {
        // Arrange
        var notification = CreateNotification(nationalIdentityNumber: _validNin);
        _serviceMock
            .Setup(s => s.GetNotificationsByNinAsync(_validNin, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DashboardNotification> { notification });

        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("nin", ("NationalIdentityNumber", _validNin));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<List<DashboardNotificationExt>>(content, _options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        var item = Assert.Single(result);
        Assert.Equal(notification.ShipmentId, item.ShipmentId);
        Assert.Equal(notification.CreatorName, item.CreatorName);
        Assert.Equal(notification.SendersReference, item.SendersReference);

        _serviceMock.Verify(s => s.GetNotificationsByNinAsync(_validNin, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByNin_MissingRequiredHeader_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("nin");

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _serviceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByNin_InvalidNinFormat_ReturnsBadRequestWithValidationError()
    {
        // Arrange
        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("nin", ("NationalIdentityNumber", "12345"));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("National identity number", content, StringComparison.OrdinalIgnoreCase);
        _serviceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByNin_NoBearerToken_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage request = CreateRequest("nin", ("NationalIdentityNumber", _validNin));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetByNin_InvalidScope_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage request = CreateRequest("nin", ("NationalIdentityNumber", _validNin));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetByNin_WithFromAndToFilters_PassesRangeToServiceAndReturnsOk()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-3);
        var to = DateTime.UtcNow.AddDays(-1);
        var notification = CreateNotification(nationalIdentityNumber: _validNin);

        _serviceMock
            .Setup(s => s.GetNotificationsByNinAsync(_validNin, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DashboardNotification> { notification });

        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest(
            $"nin?From={Uri.EscapeDataString(from.ToString("O"))}&To={Uri.EscapeDataString(to.ToString("O"))}",
            ("NationalIdentityNumber", _validNin));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _serviceMock.Verify(s => s.GetNotificationsByNinAsync(_validNin, from, to, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByNin_NoMatchingNotifications_ReturnsOkWithEmptyList()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.GetNotificationsByNinAsync(_validNin, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DashboardNotification>());

        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("nin", ("NationalIdentityNumber", _validNin));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<List<DashboardNotificationExt>>(content, _options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByNin_ServiceThrowsOperationCanceled_Returns499WithProblemDetails()
    {
        // Arrange
        var serviceMock = new Mock<IDashboardService>();
        serviceMock
            .Setup(s => s.GetNotificationsByNinAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        HttpClient client = GetTestClient(serviceMock.Object);
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("nin", ("NationalIdentityNumber", _validNin));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problemDetails = JsonSerializer.Deserialize<AltinnProblemDetails>(content, _options);

        // Assert
        Assert.Equal((HttpStatusCode)499, response.StatusCode);
        Assert.NotNull(problemDetails);
        Assert.Equal((int)response.StatusCode, problemDetails.Status);
    }

    // GET recipients/notifications/orgnumber
    [Fact]
    public async Task GetByOrgNumber_ValidRequest_ReturnsOkWithExpectedPayload()
    {
        // Arrange
        var notification = CreateNotification(organizationNumber: _validOrgNumber);
        _serviceMock
            .Setup(s => s.GetNotificationsByOrgNumberAsync(_validOrgNumber, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DashboardNotification> { notification });

        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("orgnumber", ("OrganizationNumber", _validOrgNumber));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<List<DashboardNotificationExt>>(content, _options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        var item = Assert.Single(result);
        Assert.Equal(notification.ShipmentId, item.ShipmentId);

        _serviceMock.Verify(s => s.GetNotificationsByOrgNumberAsync(_validOrgNumber, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByOrgNumber_MissingRequiredHeader_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("orgnumber");

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _serviceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByOrgNumber_InvalidOrgNumberFormat_ReturnsBadRequestWithValidationError()
    {
        // Arrange
        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("orgnumber", ("OrganizationNumber", "12345"));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Organization number", content, StringComparison.OrdinalIgnoreCase);
        _serviceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByOrgNumber_NoBearerToken_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage request = CreateRequest("orgnumber", ("OrganizationNumber", _validOrgNumber));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetByOrgNumber_InvalidScope_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage request = CreateRequest("orgnumber", ("OrganizationNumber", _validOrgNumber));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetByOrgNumber_WithFromAndToFilters_PassesRangeToServiceAndReturnsOk()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-3);
        var to = DateTime.UtcNow.AddDays(-1);
        var notification = CreateNotification(organizationNumber: _validOrgNumber);

        _serviceMock
            .Setup(s => s.GetNotificationsByOrgNumberAsync(_validOrgNumber, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DashboardNotification> { notification });

        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest(
            $"orgnumber?From={Uri.EscapeDataString(from.ToString("O"))}&To={Uri.EscapeDataString(to.ToString("O"))}",
            ("OrganizationNumber", _validOrgNumber));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _serviceMock.Verify(s => s.GetNotificationsByOrgNumberAsync(_validOrgNumber, from, to, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByOrgNumber_NoMatchingNotifications_ReturnsOkWithEmptyList()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.GetNotificationsByOrgNumberAsync(_validOrgNumber, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DashboardNotification>());

        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("orgnumber", ("OrganizationNumber", _validOrgNumber));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<List<DashboardNotificationExt>>(content, _options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByOrgNumber_ServiceThrowsOperationCanceled_Returns499WithProblemDetails()
    {
        // Arrange
        var serviceMock = new Mock<IDashboardService>();
        serviceMock
            .Setup(s => s.GetNotificationsByOrgNumberAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        HttpClient client = GetTestClient(serviceMock.Object);
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("orgnumber", ("OrganizationNumber", _validOrgNumber));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problemDetails = JsonSerializer.Deserialize<AltinnProblemDetails>(content, _options);

        // Assert
        Assert.Equal((HttpStatusCode)499, response.StatusCode);
        Assert.NotNull(problemDetails);
        Assert.Equal((int)response.StatusCode, problemDetails.Status);
    }

    // GET recipients/notifications/email
    [Fact]
    public async Task GetByEmail_ValidRequest_ReturnsOkWithExpectedPayload()
    {
        // Arrange
        var notification = CreateNotification(emailAddress: _validEmail);
        _serviceMock
            .Setup(s => s.GetNotificationsByEmailAsync(_validEmail, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DashboardNotification> { notification });

        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("email", ("Email", _validEmail));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<List<DashboardNotificationExt>>(content, _options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        var item = Assert.Single(result);
        Assert.Equal(notification.ShipmentId, item.ShipmentId);

        _serviceMock.Verify(s => s.GetNotificationsByEmailAsync(_validEmail, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByEmail_MissingRequiredHeader_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("email");

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _serviceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByEmail_InvalidEmailFormat_ReturnsBadRequestWithValidationError()
    {
        // Arrange
        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("email", ("Email", "not-an-email"));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("valid email address", content, StringComparison.OrdinalIgnoreCase);
        _serviceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByEmail_NoBearerToken_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage request = CreateRequest("email", ("Email", _validEmail));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetByEmail_InvalidScope_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage request = CreateRequest("email", ("Email", _validEmail));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetByEmail_WithFromAndToFilters_PassesRangeToServiceAndReturnsOk()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-3);
        var to = DateTime.UtcNow.AddDays(-1);
        var notification = CreateNotification(emailAddress: _validEmail);

        _serviceMock
            .Setup(s => s.GetNotificationsByEmailAsync(_validEmail, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DashboardNotification> { notification });

        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest(
            $"email?From={Uri.EscapeDataString(from.ToString("O"))}&To={Uri.EscapeDataString(to.ToString("O"))}",
            ("Email", _validEmail));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _serviceMock.Verify(s => s.GetNotificationsByEmailAsync(_validEmail, from, to, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByEmail_NoMatchingNotifications_ReturnsOkWithEmptyList()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.GetNotificationsByEmailAsync(_validEmail, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DashboardNotification>());

        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("email", ("Email", _validEmail));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<List<DashboardNotificationExt>>(content, _options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByEmail_ServiceThrowsOperationCanceled_Returns499WithProblemDetails()
    {
        // Arrange
        var serviceMock = new Mock<IDashboardService>();
        serviceMock
            .Setup(s => s.GetNotificationsByEmailAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        HttpClient client = GetTestClient(serviceMock.Object);
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("email", ("Email", _validEmail));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problemDetails = JsonSerializer.Deserialize<AltinnProblemDetails>(content, _options);

        // Assert
        Assert.Equal((HttpStatusCode)499, response.StatusCode);
        Assert.NotNull(problemDetails);
        Assert.Equal((int)response.StatusCode, problemDetails.Status);
    }

    // GET recipients/notifications/phonenumber
    [Fact]
    public async Task GetByPhoneNumber_ValidRequest_ReturnsOkWithExpectedPayload()
    {
        // Arrange
        var notification = CreateNotification(phoneNumber: _validPhoneNumber);
        _serviceMock
            .Setup(s => s.GetNotificationsByPhoneNumberAsync(_validPhoneNumber, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DashboardNotification> { notification });

        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("phonenumber", ("PhoneNumber", _validPhoneNumber));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<List<DashboardNotificationExt>>(content, _options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        var item = Assert.Single(result);
        Assert.Equal(notification.ShipmentId, item.ShipmentId);

        _serviceMock.Verify(s => s.GetNotificationsByPhoneNumberAsync(_validPhoneNumber, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByPhoneNumber_MissingRequiredHeader_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("phonenumber");

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _serviceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByPhoneNumber_InvalidPhoneNumberFormat_ReturnsBadRequestWithValidationError()
    {
        // Arrange
        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("phonenumber", ("PhoneNumber", "40000001"));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("valid mobile number", content, StringComparison.OrdinalIgnoreCase);
        _serviceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByPhoneNumber_NoBearerToken_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage request = CreateRequest("phonenumber", ("PhoneNumber", _validPhoneNumber));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetByPhoneNumber_InvalidScope_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage request = CreateRequest("phonenumber", ("PhoneNumber", _validPhoneNumber));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetByPhoneNumber_WithFromAndToFilters_PassesRangeToServiceAndReturnsOk()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-3);
        var to = DateTime.UtcNow.AddDays(-1);
        var notification = CreateNotification(phoneNumber: _validPhoneNumber);

        _serviceMock
            .Setup(s => s.GetNotificationsByPhoneNumberAsync(_validPhoneNumber, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DashboardNotification> { notification });

        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest(
            $"phonenumber?From={Uri.EscapeDataString(from.ToString("O"))}&To={Uri.EscapeDataString(to.ToString("O"))}",
            ("PhoneNumber", _validPhoneNumber));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _serviceMock.Verify(s => s.GetNotificationsByPhoneNumberAsync(_validPhoneNumber, from, to, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByPhoneNumber_NoMatchingNotifications_ReturnsOkWithEmptyList()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.GetNotificationsByPhoneNumberAsync(_validPhoneNumber, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DashboardNotification>());

        HttpClient client = GetTestClient();
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("phonenumber", ("PhoneNumber", _validPhoneNumber));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<List<DashboardNotificationExt>>(content, _options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByPhoneNumber_ServiceThrowsOperationCanceled_Returns499WithProblemDetails()
    {
        // Arrange
        var serviceMock = new Mock<IDashboardService>();
        serviceMock
            .Setup(s => s.GetNotificationsByPhoneNumberAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        HttpClient client = GetTestClient(serviceMock.Object);
        SetValidAuthorization(client);

        HttpRequestMessage request = CreateRequest("phonenumber", ("PhoneNumber", _validPhoneNumber));

        // Act
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problemDetails = JsonSerializer.Deserialize<AltinnProblemDetails>(content, _options);

        // Assert
        Assert.Equal((HttpStatusCode)499, response.StatusCode);
        Assert.NotNull(problemDetails);
        Assert.Equal((int)response.StatusCode, problemDetails.Status);
    }

    private static HttpRequestMessage CreateRequest(string pathAndQuery, params (string Name, string Value)[] headers)
    {
        HttpRequestMessage request = new(HttpMethod.Get, $"{_basePath}/recipients/notifications/{pathAndQuery}");

        foreach (var (name, value) in headers)
        {
            request.Headers.Add(name, value);
        }

        return request;
    }

    private static void SetValidAuthorization(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            PrincipalUtil.GetOrgToken("ttd", scope: _validScope));
    }

    private static DashboardNotification CreateNotification(
        string? nationalIdentityNumber = null,
        string? organizationNumber = null,
        string? emailAddress = null,
        string? phoneNumber = null)
    {
        List<DashboardDeliveryAttempt> deliveryAttempts = phoneNumber != null
            ? [new DashboardDeliveryAttempt(nationalIdentityNumber, organizationNumber, "sms", null, phoneNumber, "Delivered", DateTime.UtcNow)]
            : [new DashboardDeliveryAttempt(nationalIdentityNumber, organizationNumber, "email", emailAddress ?? "recipient@example.com", null, "Delivered", DateTime.UtcNow)];

        return new(
            Guid.NewGuid(),
            "digdir",
            "urn:altinn:resource:some-app",
            "senders-ref-001",
            DateTime.UtcNow.AddDays(-1),
            phoneNumber != null ? NotificationChannel.Sms : NotificationChannel.Email,
            "notification",
            deliveryAttempts);
    }

    private HttpClient GetTestClient(IDashboardService? service = null)
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
}
