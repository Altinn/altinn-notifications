using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Models.InstantMessage;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Moq;

namespace Altinn.Notifications.Sms.IntegrationTests.InstantMessageController;

public class InstantMessageControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.InstantMessageController>>
{
    private readonly IntegrationTestWebApplicationFactory<Controllers.InstantMessageController> _factory;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public InstantMessageControllerTests(IntegrationTestWebApplicationFactory<Controllers.InstantMessageController> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void InstantMessageController_HasApiExplorerSettingsWithIgnoreApiTrue()
    {
        // Arrange
        var controllerType = typeof(Controllers.InstantMessageController);

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
        sendingServiceMock.Setup(s => s.SendAsync(It.IsAny<Core.Sending.Sms>(), It.IsAny<int>())).ThrowsAsync(new OperationCanceledException());

        var instantMessageRequest = new InstantMessageRequest
        {
            TimeToLive = 360,
            Sender = "TestService",
            Message = "OTP: 088E863A",
            Recipient = "+4799999999",
            NotificationId = Guid.NewGuid()
        };

        var httpClient = GetTestClient(sendingServiceMock.Object);

        // Act
        var response = await httpClient.PostAsJsonAsync("/notifications/sms/api/v1/instantmessage/send", instantMessageRequest);

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
        sendingServiceMock.Setup(e => e.SendAsync(It.IsAny<Core.Sending.Sms>(), It.IsAny<int>())).ThrowsAsync(new InvalidOperationException());

        var instantMessageRequest = new InstantMessageRequest
        {
            TimeToLive = 21600,
            Sender = "TestService",
            Recipient = "+4799999999",
            NotificationId = notificationId,
            Message = "Your one time password is: 2A31519EC7C6",
        };

        var httpClient = GetTestClient(sendingServiceMock.Object);

        // Act
        var response = await httpClient.PostAsJsonAsync("/notifications/sms/api/v1/instantmessage/send", instantMessageRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, _jsonOptions);

        Assert.NotNull(problemDetails);
        Assert.Equal(400, problemDetails.Status);
    }

    [Theory]
    [InlineData("", "", "", "", 0)] // All invalid
    [InlineData("Altinn", "+4799999999", "", "Valid message", 360)] // Invalid NotificationId
    [InlineData("Altinn", "+4799999999", "00000000-0000-0000-0000-000000000000", "", 360)] // Invalid Message
    [InlineData("Altinn", "", "00000000-0000-0000-0000-000000000000", "Valid message", 360)] // Invalid Recipient
    [InlineData("", "+4799999999", "00000000-0000-0000-0000-000000000000", "Valid message", 360)] // Invalid Sender
    [InlineData("Altinn", "+4799999999", "00000000-0000-0000-0000-000000000000", "Valid message", 0)] // Invalid TimeToLive (below min)
    [InlineData("Altinn", "+4799999999", "00000000-0000-0000-0000-000000000000", "Valid message", 172801)] // Invalid TimeToLive (above max)
    public async Task Send_WithInvalidRequestProperties_ReturnsBadRequestWithValidationError(string sender, string recipient, string notificationId, string message, int timeToLive)
    {
        // Arrange
        var sendingServiceMock = new Mock<ISendingService>();
        var httpClient = GetTestClient(sendingServiceMock.Object);

        var jsonRequest = $$"""
        {
           "sender": "{{sender}}",
           "message": "{{message}}",
           "timeToLive": {{timeToLive}},
           "recipient": "{{recipient}}",
           "notificationId": "{{notificationId}}"
        }
        """;

        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        // Act
        var response = await httpClient.PostAsync("/notifications/sms/api/v1/instantmessage/send", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, _jsonOptions);

        Assert.NotNull(problemDetails);
        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
    }

    [Fact]
    public async Task Send_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var notificationId = Guid.NewGuid();

        var sendingServiceMock = new Mock<ISendingService>();
        sendingServiceMock.Setup(e => e.SendAsync(It.Is<Core.Sending.Sms>(e => e.NotificationId == notificationId))).Returns(Task.CompletedTask);

        var oneTimePasswordRequest = new InstantMessageRequest
        {
            TimeToLive = 7200,
            Sender = "TestService",
            Recipient = "+4799999999",
            NotificationId = notificationId,
            Message = "Your one-time password is: 2A31519EC7C6",
        };

        var httpClient = GetTestClient(sendingServiceMock.Object);

        // Act
        var response = await httpClient.PostAsJsonAsync("/notifications/sms/api/v1/instantmessage/send", oneTimePasswordRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
