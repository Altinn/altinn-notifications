using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Models.InstantEmail;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using Xunit;

namespace Altinn.Notifications.Email.IntegrationTests.InstantEmailController;

public class InstantEmailControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.InstantEmailController>>
{
    private readonly IntegrationTestWebApplicationFactory<Controllers.InstantEmailController> _factory;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public InstantEmailControllerTests(IntegrationTestWebApplicationFactory<Controllers.InstantEmailController> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void InstantEmailController_HasApiExplorerSettingsWithIgnoreApiTrue()
    {
        // Arrange
        var controllerType = typeof(Controllers.InstantEmailController);

        // Act
        var attribute = controllerType.GetCustomAttribute<ApiExplorerSettingsAttribute>();

        // Assert
        Assert.NotNull(attribute);
        Assert.True(attribute.IgnoreApi);
    }

    [Fact]
    public async Task Send_WhenRequestIsCanceled_Returns499ClientClosedRequest()
    {
        // Arrange
        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock.Setup(s => s.SendAsync(It.IsAny<Core.Sending.Email>())).ThrowsAsync(new OperationCanceledException());

        var instantEmailRequest = new InstantEmailRequest
        {
            Sender = "noreply@test.altinn.no",
            Recipient = "test@example.com",
            Subject = "Test Email",
            Body = "This is a test email message.",
            ContentType = EmailContentType.Plain,
            NotificationId = Guid.NewGuid()
        };

        var httpClient = GetTestClient(sendingServiceMock.Object);

        // Act
        var response = await httpClient.PostAsJsonAsync("/notifications/email/api/v1/instantemail", instantEmailRequest);

        // Assert
        Assert.Equal((HttpStatusCode)499, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, _jsonOptions);

        Assert.NotNull(problemDetails);
        Assert.Equal(499, problemDetails.Status);
        Assert.Contains("Request terminated", problemDetails.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Send_WhenSendingServiceFails_ReturnsBadRequest()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock.Setup(e => e.SendAsync(It.IsAny<Core.Sending.Email>())).ThrowsAsync(new InvalidOperationException());

        var instantEmailRequest = new InstantEmailRequest
        {
            Sender = "noreply@test.altinn.no",
            Recipient = "test@example.com",
            Subject = "Test Email Subject",
            Body = "This is a test email with detailed content for testing purposes.",
            ContentType = EmailContentType.Html,
            NotificationId = notificationId
        };

        var httpClient = GetTestClient(sendingServiceMock.Object);

        // Act
        var response = await httpClient.PostAsJsonAsync("/notifications/email/api/v1/instantemail", instantEmailRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, _jsonOptions);

        Assert.NotNull(problemDetails);
        Assert.Equal(400, problemDetails.Status);
    }

    [Theory]
    [InlineData("", "", "", "", "")] // All invalid
    [InlineData("noreply@test.altinn.no", "test@example.com", "", "Valid body", "Plain")] // Invalid Subject
    [InlineData("noreply@test.altinn.no", "test@example.com", "Valid subject", "", "Plain")] // Invalid Body
    [InlineData("noreply@test.altinn.no", "", "Valid subject", "Valid body", "Plain")] // Invalid Recipient
    [InlineData("", "test@example.com", "Valid subject", "Valid body", "Plain")] // Invalid Sender
    [InlineData("noreply@test.altinn.no", "invalid-email", "Valid subject", "Valid body", "Plain")] // Invalid email format
    [InlineData("noreply@test.altinn.no", "test@example.com", "Valid subject", "Valid body", "")] // Invalid ContentType
    public async Task Send_WithInvalidRequestProperties_ReturnsBadRequestWithValidationError(string sender, string recipient, string subject, string body, string contentType)
    {
        // Arrange
        var sendingServiceMock = new Mock<ISendingService>();
        var httpClient = GetTestClient(sendingServiceMock.Object);

        var jsonRequest = $$"""
        {
           "sender": "{{sender}}",
           "recipient": "{{recipient}}",
           "subject": "{{subject}}",
           "body": "{{body}}",
           "contentType": "{{contentType}}",
           "notificationId": "00000000-0000-0000-0000-000000000000"
        }
        """;

        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.PostAsync("/notifications/email/api/v1/instantemail", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, _jsonOptions);

        Assert.NotNull(problemDetails);
        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
    }

    [Fact]
    public async Task Send_WithValidPlainTextRequest_ReturnsAccepted()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock.Setup(e => e.SendAsync(It.Is<Core.Sending.Email>(e => e.NotificationId == notificationId))).Returns(Task.CompletedTask);

        var instantEmailRequest = new InstantEmailRequest
        {
            Sender = "noreply@test.altinn.no",
            Recipient = "test@example.com",
            Subject = "Test Notification",
            Body = "This is a plain text email notification sent instantly.",
            ContentType = EmailContentType.Plain,
            NotificationId = notificationId
        };

        var httpClient = GetTestClient(sendingServiceMock.Object);

        // Act
        var response = await httpClient.PostAsJsonAsync("/notifications/email/api/v1/instantemail", instantEmailRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        sendingServiceMock.Verify(e => e.SendAsync(It.IsAny<Core.Sending.Email>()), Times.Once);
    }

    [Fact]
    public async Task Send_WithValidHtmlRequest_ReturnsAccepted()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock.Setup(e => e.SendAsync(It.Is<Core.Sending.Email>(e => e.NotificationId == notificationId))).Returns(Task.CompletedTask);

        var instantEmailRequest = new InstantEmailRequest
        {
            Sender = "noreply@test.altinn.no",
            Recipient = "test@example.com",
            Subject = "HTML Test Notification",
            Body = "<html><body><h1>Test Email</h1><p>This is an <strong>HTML</strong> email notification.</p></body></html>",
            ContentType = EmailContentType.Html,
            NotificationId = notificationId
        };

        var httpClient = GetTestClient(sendingServiceMock.Object);

        // Act
        var response = await httpClient.PostAsJsonAsync("/notifications/email/api/v1/instantemail", instantEmailRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        sendingServiceMock.Verify(e => e.SendAsync(It.IsAny<Core.Sending.Email>()), Times.Once);
    }

    [Fact]
    public async Task Send_WhenCalled_MapsRequestToDomainEmail()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        Core.Sending.Email? capturedEmail = null;

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock.Setup(e => e.SendAsync(It.IsAny<Core.Sending.Email>()))
            .Callback<Core.Sending.Email>(email => capturedEmail = email)
            .Returns(Task.CompletedTask);

        var instantEmailRequest = new InstantEmailRequest
        {
            Sender = "sender@test.altinn.no",
            Recipient = "recipient@example.com",
            Subject = "Mapping Test Subject",
            Body = "Test body content for mapping verification",
            ContentType = EmailContentType.Html,
            NotificationId = notificationId
        };

        var httpClient = GetTestClient(sendingServiceMock.Object);

        // Act
        await httpClient.PostAsJsonAsync("/notifications/email/api/v1/instantemail", instantEmailRequest);

        // Assert
        Assert.NotNull(capturedEmail);
        Assert.Equal(notificationId, capturedEmail.NotificationId);
        Assert.Equal("Mapping Test Subject", capturedEmail.Subject);
        Assert.Equal("Test body content for mapping verification", capturedEmail.Body);
        Assert.Equal("sender@test.altinn.no", capturedEmail.FromAddress);
        Assert.Equal("recipient@example.com", capturedEmail.ToAddress);
        Assert.Equal(EmailContentType.Html, capturedEmail.ContentType);
        sendingServiceMock.Verify(e => e.SendAsync(It.IsAny<Core.Sending.Email>()), Times.Once);
    }

    [Fact]
    public async Task Send_WithStringEnumContentType_ReturnsAccepted()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock.Setup(e => e.SendAsync(It.IsAny<Core.Sending.Email>())).Returns(Task.CompletedTask);

        var httpClient = GetTestClient(sendingServiceMock.Object);

        var jsonRequest = $$"""
        {
           "sender": "noreply@test.altinn.no",
           "recipient": "test@example.com",
           "subject": "String Enum Test",
           "body": "Testing string enum contentType",
           "contentType": "Html",
           "notificationId": "{{notificationId}}"
        }
        """;

        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.PostAsync("/notifications/email/api/v1/instantemail", content);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        sendingServiceMock.Verify(e => e.SendAsync(It.IsAny<Core.Sending.Email>()), Times.Once);
    }

    private HttpClient GetTestClient(ISendingService sendingService)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(sendingService);
            });
        }).CreateClient();
    }
}
