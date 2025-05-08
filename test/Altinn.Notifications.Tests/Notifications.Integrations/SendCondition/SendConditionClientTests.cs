using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using Altinn.Notifications.Core.Models.SendCondition;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.SendCondition;
using Altinn.Notifications.IntegrationTests;

using Moq;
using Moq.Protected;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.SendCondition
{
    public class SendConditionClientTests
    {
        private readonly JsonSerializerOptions _serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly DelegatingHandlerStub _messageHandler;
        private readonly SendConditionClient _sendConditionClient;

        public SendConditionClientTests()
        {
            _messageHandler = new DelegatingHandlerStub(async (request, token) =>
            {
                await Task.CompletedTask;
                string? desiredResponse = HttpUtility.ParseQueryString(request!.RequestUri!.Query)["desiredResponse"];

                return desiredResponse switch
                {
                    "true" => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(new SendConditionResponse { SendNotification = true }, _serializerOptions))
                    },
                    "false" => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(new SendConditionResponse { SendNotification = false }, _serializerOptions))
                    },
                    "oknobody" => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"isValidJson\": true}")
                    },
                    "invalidbody" => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("This is not a valid JSON")
                    },
                    "badrequest" => new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent("Bad request")
                    },
                    "emptybody" => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(string.Empty)
                    },
                    "nullnotification" => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(new SendConditionResponse { SendNotification = null }, _serializerOptions))
                    },
                    "readasyncfails" => throw new Exception("ReadAsStringAsync failed"),
                    _ => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(new SendConditionResponse { SendNotification = true }, _serializerOptions))
                    },
                };
            });

            _sendConditionClient = new SendConditionClient(new HttpClient(_messageHandler));
        }

        [Fact]
        public async Task CheckSendCondition_SendConditionTrue_ReturnsTrue()
        {
            // Act
            Result<bool, ConditionClientError> result = await _sendConditionClient.CheckSendCondition(new Uri("http://test.com?desiredResponse=true"));

            // Assert
            result.Match(
                sendNotification =>
                {
                    Assert.True(sendNotification);

                    return true;
                },
                error =>
                {
                    throw new Exception("No error value should be returned if send condition response is true");
                });
        }

        [Fact]
        public async Task CheckSendCondition_SendContitionFalse_ReturnsFalse()
        {
            // Act
            Result<bool, ConditionClientError> result = await _sendConditionClient.CheckSendCondition(new Uri("http://test.com?desiredResponse=false"));

            // Assert
            result.Match(
                sendNotification =>
                {
                    Assert.False(sendNotification);

                    return true;
                },
                error =>
                {
                    throw new Exception("No error value should be returned if send condition response is false");
                });
        }

        [Fact]
        public async Task CheckSendCondition_InvalidJson_ReturnsClientError()
        {
            // Act
            Result<bool, ConditionClientError> result = await _sendConditionClient.CheckSendCondition(new Uri("http://test.com?desiredResponse=invalidbody"));

            // Assert
            result.Match(
                sendNotification =>
                {
                    throw new Exception("No success value should be returned if json deserialization fails");
                },
                error =>
                {
                    Assert.False(string.IsNullOrEmpty(error.Message));

                    Assert.Contains("Deserialization into SendConditionResponse failed", error.Message);

                    return true;
                });
        }

        [Fact]
        public async Task CheckSendCondition_BadRequestResponse_ReturnsClientError()
        {
            // Act
            Result<bool, ConditionClientError> result = await _sendConditionClient.CheckSendCondition(new Uri("http://test.com?desiredResponse=badrequest"));

            // Assert
            result.Match(
                sendNotification =>
                {
                    throw new Exception("No success value should be returned if non success code is returned");
                },
                error =>
                {
                    Assert.Equal(400, error.StatusCode);
                    Assert.Contains("Unsuccessful response", error.Message);

                    return true;
                });
        }

        [Fact]
        public async Task CheckSendCondition_OkResponseWithoutBody_ReturnsClientError()
        {
            // Act
            Result<bool, ConditionClientError> result = await _sendConditionClient.CheckSendCondition(new Uri("http://test.com?desiredResponse=oknobody"));

            // Assert
            result.Match(
                sendNotification =>
                {
                    throw new Exception("No success value should be returned if non success code is returned");
                },
                error =>
                {
                    Assert.False(string.IsNullOrEmpty(error.Message));
                    Assert.Contains("No condition response in the body", error.Message);

                    return true;
                });
        }

        [Fact]
        public async Task CheckSendCondition_EmptyResponseBody_ReturnsClientError()
        {
            // Act
            Result<bool, ConditionClientError> result = await _sendConditionClient.CheckSendCondition(new Uri("http://test.com?desiredResponse=emptybody"));

            // Assert
            result.Match(
                sendNotification =>
                {
                    throw new Exception("No success value should be returned for empty response body");
                },
                actualError =>
                {
                    Assert.Equal("Response body is empty", actualError.Message);
                    Assert.Equal(200, actualError.StatusCode);
                    return true;
                });
        }

        [Fact]
        public async Task CheckSendCondition_NullNotificationValue_ReturnsClientError()
        {
            // Act
            Result<bool, ConditionClientError> result = await _sendConditionClient.CheckSendCondition(new Uri("http://test.com?desiredResponse=nullnotification"));

            // Assert
            result.Match(
                sendNotification =>
                {
                    throw new Exception("No success value should be returned when SendNotification is null");
                },
                actualError =>
                {
                    Assert.Contains("No condition response in the body", actualError.Message);
                    Assert.Equal(200, actualError.StatusCode);

                    return true;
                });
        }

        [Fact]
        public async Task CheckSendCondition_HttpRequestException_ReturnsClientError()
        {
            // Arrange
            var mockHandler = new Mock<DelegatingHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Connection refused"));

            var client = new SendConditionClient(new HttpClient(mockHandler.Object));

            // Act
            Result<bool, ConditionClientError> result = await client.CheckSendCondition(new Uri("http://test.com"));

            // Assert
            result.Match(
                sendNotification =>
                {
                    throw new Exception("No success value should be returned when HttpRequestException occurs");
                },
                actualError =>
                {
                    Assert.Contains("HTTP request failed", actualError.Message);
                    return true;
                });
        }

        [Fact]
        public async Task CheckSendCondition_TaskCanceledException_ReturnsClientError()
        {
            // Arrange
            var mockHandler = new Mock<DelegatingHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("Request timed out"));

            var client = new SendConditionClient(new HttpClient(mockHandler.Object));

            // Act
            Result<bool, ConditionClientError> result = await client.CheckSendCondition(new Uri("http://test.com"));

            // Assert
            result.Match(
                sendNotification =>
                {
                    throw new Exception("No success value should be returned when TaskCanceledException occurs");
                },
                actualError =>
                {
                    Assert.Contains("Request timed out", actualError.Message);
                    return true;
                });
        }

        [Fact]
        public async Task CheckSendCondition_GenericException_ReturnsClientError()
        {
            // Arrange
            var mockHandler = new Mock<DelegatingHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Unexpected error"));

            var client = new SendConditionClient(new HttpClient(mockHandler.Object));

            // Act
            Result<bool, ConditionClientError> result = await client.CheckSendCondition(new Uri("http://test.com"));

            // Assert
            result.Match(
                sendNotification =>
                {
                    throw new Exception("No success value should be returned when generic exception occurs");
                },
                actualError =>
                {
                    Assert.Contains("Unexpected error during HTTP request", actualError.Message);
                    return true;
                });
        }

        [Fact]
        public async Task CheckSendCondition_NoContentStatus_ReturnsClientErrorWithStatusCode204()
        {
            // Arrange
            var handler = new DelegatingHandlerStub((request, token) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.NoContent)
                {
                    Content = new StringContent(string.Empty)
                };
                return Task.FromResult(response);
            });
            var client = new SendConditionClient(new HttpClient(handler));

            // Act
            var result = await client.CheckSendCondition(new Uri("http://test.com"));

            // Assert
            var asserted = result.Match(
                success =>
                {
                    throw new Exception("Should not succeed when no content is returned");
                },
                error =>
                {
                    Assert.Equal(204, error.StatusCode);
                    Assert.Equal("Response body is empty", error.Message);

                    return true;
                });

            Assert.True(asserted);
        }
    }
}
