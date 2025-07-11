using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Helpers;
using Altinn.Notifications.Core.Models.ShortMessageService;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.ShortMessageService;
using Altinn.Notifications.IntegrationTests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.ShortMessageService;

public class ShortMessageServiceClientTests
{
    private readonly ShortMessageServiceClient _shortMessageServiceClient;
    private readonly Mock<ILogger<ShortMessageServiceClient>> _loggerMock;

    public ShortMessageServiceClientTests()
    {
        _loggerMock = new Mock<ILogger<ShortMessageServiceClient>>();
        _shortMessageServiceClient = CreateShortMessageServiceTestClient();
    }

    [Fact]
    public async Task SendAsync_ValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var shortMessage = new ShortMessage
        {
            Sender = "Altinn",
            TimeToLive = 3600,
            Message = "Test message",
            Recipient = "+4799999999",
            NotificationId = Guid.NewGuid()
        };

        // Act
        var result = await _shortMessageServiceClient.SendAsync(shortMessage);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorDetails);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    [Fact]
    public async Task SendAsync_BadRequest_ReturnsFailureWithErrorDetails()
    {
        // Arrange
        var shortMessage = new ShortMessage
        {
            Sender = "Altinn",
            TimeToLive = 7200,
            Message = "Test message",
            NotificationId = Guid.NewGuid(),
            Recipient = "invalid-mobile-number"
        };

        // Act
        var result = await _shortMessageServiceClient.SendAsync(shortMessage);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("Invalid request format", result.ErrorDetails);

        _loggerMock.Verify(e => e.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ClientClosedRequest_ReturnsFailureWithErrorDetails()
    {
        // Arrange
        var shortMessage = new ShortMessage
        {
            Sender = "Altinn",
            TimeToLive = 3600,
            Message = "Test message",
            Recipient = "client-closed",
            NotificationId = Guid.NewGuid()
        };

        // Act
        var result = await _shortMessageServiceClient.SendAsync(shortMessage);

        // Assert
        Assert.False(result.Success);
        Assert.Equal((HttpStatusCode)499, result.StatusCode);
        Assert.Contains("Request was canceled", result.ErrorDetails);
    }

    [Fact]
    public async Task SendAsync_GenericException_ReturnsFailureWithExceptionDetails()
    {
        // Arrange
        var shortMessage = new ShortMessage
        {
            Sender = "Altinn",
            TimeToLive = 3600,
            Message = "Test message",
            Recipient = "generic-error",
            NotificationId = Guid.NewGuid()
        };

        // Act
        var result = await _shortMessageServiceClient.SendAsync(shortMessage);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Contains("An unexpected error occurred", result.ErrorDetails);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_HttpRequestException_ReturnsFailureWithExceptionDetails()
    {
        // Arrange
        var shortMessage = new ShortMessage
        {
            Sender = "Altinn",
            TimeToLive = 3600,
            Message = "Test message",
            Recipient = "network-error",
            NotificationId = Guid.NewGuid()
        };

        // Act
        var result = await _shortMessageServiceClient.SendAsync(shortMessage);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Service unavailable", result.ErrorDetails);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);

        _loggerMock.Verify(
            e => e.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_CancellationTokenCancelsDuringExecution_ThrowsTaskCanceledException()
    {
        // Arrange
        var shortMessage = new ShortMessage
        {
            Sender = "Altinn",
            TimeToLive = 3600,
            Message = "Test message",
            Recipient = "+4799999999",
            NotificationId = Guid.NewGuid()
        };

        // Use a handler that waits before returning to allow cancellation
        var handler = new DelegatingHandlerStub(async (request, token) =>
        {
            await Task.Delay(1000, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var settings = new PlatformSettings
        {
            ApiShortMessageServiceEndpoint = "http://localhost:5092/notifications/sms/api/v1/"
        };

        var client = new ShortMessageServiceClient(new HttpClient(handler), _loggerMock.Object, Options.Create(settings));

        using var cts = new CancellationTokenSource();
        var sendTask = client.SendAsync(shortMessage, cts.Token);

        // Cancel after a short delay
        cts.CancelAfter(100);

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await sendTask);
    }

    private ShortMessageServiceClient CreateShortMessageServiceTestClient()
    {
        JsonSerializerOptions serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var delegatingHandler = new DelegatingHandlerStub((request, token) =>
        {
            if (!request!.RequestUri!.AbsolutePath.EndsWith("instantmessage/send"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            ShortMessage? message = null;
            try
            {
                string content = request.Content!.ReadAsStringAsync(token).GetAwaiter().GetResult();
                message = JsonSerializer.Deserialize<ShortMessage>(content, serializerOptions);
            }
            catch
            {
                /* Ignore deserialization errors for this test */
            }

            if (message == null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Invalid JSON format", Encoding.UTF8, "application/json")
                });
            }

            HttpStatusCode statusCode;
            string? errorContent = null;
            switch (message.Recipient)
            {
                case "client-closed":
                    statusCode = (HttpStatusCode)499;
                    errorContent = "{\"title\":\"Request terminated\",\"status\":499,\"detail\":\"Request was canceled before processing could complete\"}";
                    break;

                case "network-error":
                    throw new HttpRequestException("Service unavailable", null, HttpStatusCode.ServiceUnavailable);

                case "generic-error":
                    throw new InvalidOperationException("Some unexpected error occurred");

                default:
                    if (MobileNumberHelper.IsValidMobileNumber(message.Recipient))
                    {
                        statusCode = HttpStatusCode.OK;
                    }
                    else
                    {
                        statusCode = HttpStatusCode.BadRequest;
                        errorContent = "{\"title\":\"Invalid request format\",\"status\":400,\"detail\":\"The phone number format is invalid\"}";
                    }

                    break;
            }

            var response = new HttpResponseMessage(statusCode);
            if (errorContent != null)
            {
                response.Content = new StringContent(errorContent, Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        });

        PlatformSettings settings = new()
        {
            ApiShortMessageServiceEndpoint = "http://localhost:5092/notifications/sms/api/v1/"
        };

        return new ShortMessageServiceClient(new HttpClient(delegatingHandler), _loggerMock.Object, Options.Create(settings));
    }
}
