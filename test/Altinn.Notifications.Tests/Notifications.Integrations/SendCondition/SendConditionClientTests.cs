using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

using Altinn.Notifications.Core.Models.SendCondition;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Integrations.SendCondition;
using Altinn.Notifications.IntegrationTests;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Integrations.SendCondition
{
    public class SendConditionClientTests
    {
        private readonly JsonSerializerOptions _serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly SendConditionClient _sendConditionClient;

        public SendConditionClientTests()
        {
            var messageHandler = new DelegatingHandlerStub(async (request, token) =>
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
                    "oknobody" => new HttpResponseMessage(HttpStatusCode.OK),
                    "invalidbody" => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("This is not a valid JSON")
                    },
                    "badrequest" => new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent("Bad request")
                    },
                    _ => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(new SendConditionResponse { SendNotification = true }, _serializerOptions))
                    },
                };
            });

            _sendConditionClient = new SendConditionClient(new HttpClient(messageHandler));
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
                actuallError => throw new Exception("No error value should be returned if send condition response is true"));
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
                actuallError => throw new Exception("No error value should be returned if send condition response is false"));
        }

        [Fact]
        public async Task CheckSendCondition_InvalidJson_ReturnsClientError()
        {
            // Act
            Result<bool, ConditionClientError> result = await _sendConditionClient.CheckSendCondition(new Uri("http://test.com?desiredResponse=invalidbody"));

            // Assert
            result.Match(
                sendNotification => throw new Exception("No success value should be returned if json deserialization fails"),
                actuallError =>
                {
                    Assert.False(string.IsNullOrEmpty(actuallError?.Message));
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
                sendNotification => throw new Exception("No success value should be returned if non success code is returned"),
                actuallError =>
                {
                    Assert.Equal(400, actuallError?.StatusCode);
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
                sendNotification => throw new Exception("No success value should be returned if non success code is returned"),
                actuallError =>
                {
                    Assert.False(string.IsNullOrEmpty(actuallError?.Message));
                    return true;
                });
        }        
    }
}
