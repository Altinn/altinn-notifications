using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.InstantEmailService;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Integrations.InstantEmailService;
using Altinn.Notifications.IntegrationTests;
using Altinn.Notifications.Validators.Rules;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.InstantEmailService;

public class InstantEmailServiceClientTests
{
    private readonly InstantEmailServiceClient _instantEmailServiceClient;
    private readonly Mock<ILogger<InstantEmailServiceClient>> _loggerMock;

    public InstantEmailServiceClientTests()
    {
        _loggerMock = new Mock<ILogger<InstantEmailServiceClient>>();
        _instantEmailServiceClient = CreateInstantEmailServiceTestClient();
    }

    [Fact]
    public async Task SendAsync_ValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var instantEmail = new InstantEmail
        {
            Sender = "test@altinn.no",
            Recipient = "user@example.com",
            Subject = "Test Subject",
            Body = "Test email body",
            ContentType = EmailContentType.Plain,
            NotificationId = Guid.NewGuid()
        };

        // Act
        var result = await _instantEmailServiceClient.SendAsync(instantEmail);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorDetails);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    [Fact]
    public async Task SendAsync_BadRequest_ReturnsFailureWithErrorDetails()
    {
        // Arrange
        var instantEmail = new InstantEmail
        {
            Sender = "test@altinn.no",
            Recipient = "invalid-email-address",
            Subject = "Test Subject",
            Body = "Test email body",
            ContentType = EmailContentType.Html,
            NotificationId = Guid.NewGuid()
        };

        // Act
        var result = await _instantEmailServiceClient.SendAsync(instantEmail);

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
        var instantEmail = new InstantEmail
        {
            Sender = "test@altinn.no",
            Recipient = "client-closed@example.com",
            Subject = "Test Subject",
            Body = "Test email body",
            ContentType = EmailContentType.Plain,
            NotificationId = Guid.NewGuid()
        };

        // Act
        var result = await _instantEmailServiceClient.SendAsync(instantEmail);

        // Assert
        Assert.False(result.Success);
        Assert.Equal((HttpStatusCode)499, result.StatusCode);
        Assert.Contains("Request was canceled", result.ErrorDetails);
    }

    [Fact]
    public async Task SendAsync_GenericException_ReturnsFailureWithExceptionDetails()
    {
        // Arrange
        var instantEmail = new InstantEmail
        {
            Sender = "test@altinn.no",
            Recipient = "generic-error@example.com",
            Subject = "Test Subject",
            Body = "Test email body",
            ContentType = EmailContentType.Html,
            NotificationId = Guid.NewGuid()
        };

        // Act
        var result = await _instantEmailServiceClient.SendAsync(instantEmail);

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
        var instantEmail = new InstantEmail
        {
            Sender = "test@altinn.no",
            Recipient = "network-error@example.com",
            Subject = "Test Subject",
            Body = "Test email body",
            ContentType = EmailContentType.Plain,
            NotificationId = Guid.NewGuid()
        };

        // Act
        var result = await _instantEmailServiceClient.SendAsync(instantEmail);

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

    private InstantEmailServiceClient CreateInstantEmailServiceTestClient()
    {
        JsonSerializerOptions serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        var delegatingHandler = new DelegatingHandlerStub((request, token) =>
        {
            if (!request!.RequestUri!.AbsolutePath.EndsWith("instantemail"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            InstantEmail? email = null;

            try
            {
                string content = request.Content!.ReadAsStringAsync(token).GetAwaiter().GetResult();
                email = JsonSerializer.Deserialize<InstantEmail>(content, serializerOptions);
            }
            catch
            {
                /* Ignore deserialization errors for this test */
            }

            if (email == null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Invalid JSON format", Encoding.UTF8, "application/json")
                });
            }

            HttpStatusCode statusCode;
            string? errorContent = null;

            switch (email.Recipient)
            {
                case "client-closed@example.com":
                    statusCode = (HttpStatusCode)499;
                    errorContent = "{\"title\":\"Request terminated\",\"status\":499,\"detail\":\"Request was canceled before processing could complete\"}";
                    break;

                case "network-error@example.com":
                    throw new HttpRequestException("Service unavailable", null, HttpStatusCode.ServiceUnavailable);

                case "generic-error@example.com":
                    throw new InvalidOperationException("Some unexpected error occurred");

                default:
                    if (IsValidEmailAddress(email.Recipient))
                    {
                        statusCode = HttpStatusCode.OK;
                    }
                    else
                    {
                        statusCode = HttpStatusCode.BadRequest;
                        errorContent = "{\"title\":\"Invalid request format\",\"status\":400,\"detail\":\"The email address format is invalid\"}";
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
            ApiInstantEmailServiceEndpoint = "http://localhost:5092/notifications/email/api/v1/"
        };

        return new InstantEmailServiceClient(new HttpClient(delegatingHandler), _loggerMock.Object, Options.Create(settings));
    }

    private static bool IsValidEmailAddress(string email)
    {
        return RecipientRules.IsValidEmail(email) && !email.StartsWith("invalid-");
    }
}
