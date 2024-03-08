using System.Net;
using System.Text;
using Altinn.Notifications.Email.Configuration;
using Altinn.Notifications.Email.Core.Sending;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Moq;
using Xunit;

namespace Altinn.Notifications.Email.IntegrationTests.DeliveryReportController;

public class DeliveryReportControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.DeliveryReportController>>
{
    private const string _basePath = "/notifications/email/api/v1/reports";
    private const string _queryString = "?accesskey=accesskey";

    private readonly string _deliveryEvent = "[{\"id\": \"00000000-0000-0000-0000-000000000000\",\"topic\": \"/subscriptions/{subscription-id}/resourceGroups/{group-name}/providers/microsoft.communication/communicationservices/{communication-services-resource-name}\", \"subject\": \"sender/senderid@azure.com/message/00000000-0000-0000-0000-000000000000\", \"data\": {\"sender\": \"senderid@azure.com\", \"recipient\": \"receiver@azure.com\", \"messageId\": \"00000000-0000-0000-0000-000000000000\",\"status\": \"Delivered\", \"deliveryStatusDetails\": {\"statusMessage\": \"Status Message\"},\"deliveryAttemptTimeStamp\": \"2020-09-18T00:22:20.2855749+00:00\"},\"eventType\": \"Microsoft.Communication.EmailDeliveryReportReceived\",\"dataVersion\": \"1.0\",\"metadataVersion\": \"1\",\"eventTime\": \"2020-09-18T00:22:20.822Z\"}]";
    private readonly string _deliveryEventInvalidStatusCode = "[{\"id\": \"00000000-0000-0000-0000-000000000000\",\"topic\": \"/subscriptions/{subscription-id}/resourceGroups/{group-name}/providers/microsoft.communication/communicationservices/{communication-services-resource-name}\", \"subject\": \"sender/senderid@azure.com/message/00000000-0000-0000-0000-000000000000\", \"data\": {\"sender\": \"senderid@azure.com\", \"recipient\": \"receiver@azure.com\", \"messageId\": \"00000000-0000-0000-0000-000000000000\",\"status\": \"Unhandled\", \"deliveryStatusDetails\": {\"statusMessage\": \"Status Message\"},\"deliveryAttemptTimeStamp\": \"2020-09-18T00:22:20.2855749+00:00\"},\"eventType\": \"Microsoft.Communication.EmailDeliveryReportReceived\",\"dataVersion\": \"1.0\",\"metadataVersion\": \"1\",\"eventTime\": \"2020-09-18T00:22:20.822Z\"}]";
    private readonly string _validationEvent = "[{\"id\": \"2d1781af-3a4c-4d7c-bd0c-e34b19da4e66\",\"topic\": \"/subscriptions/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx\",\"subject\": \"\",\"data\": {\"validationCode\": \"512d38b6-c7b8-40c8-89fe-f46f9e9622b6\",\"validationUrl\": \"https://rp-eastus2.eventgrid.azure.net:553/eventsubscriptions/myeventsub/validate?id=0000000000-0000-0000-0000-00000000000000&t=2022-10-28T04:23:35.1981776Z&apiVersion=2018-05-01-preview&token=1A1A1A1A\"},\"eventType\": \"Microsoft.EventGrid.SubscriptionValidationEvent\",\"eventTime\": \"2022-10-28T04:23:35.1981776Z\",\"metadataVersion\": \"1\",\"dataVersion\": \"1\"}]";

    private readonly IntegrationTestWebApplicationFactory<Controllers.DeliveryReportController> _factory;

    public DeliveryReportControllerTests(IntegrationTestWebApplicationFactory<Controllers.DeliveryReportController> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_MissingAccessKey_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(_validationEvent, Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_InvalidDeliveryReport_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent("{something:wrong}", Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidDeliveryReport_InvalidStatusCode_ReturnsInternalServerError()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath + _queryString)
        {
            Content = new StringContent(_deliveryEventInvalidStatusCode, Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        
        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidDeliveryReport_ReturnsOK()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath + _queryString)
        {
            Content = new StringContent(_deliveryEvent, Encoding.UTF8, "application/json"),
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidationEvent_ReturnsValidationResponse()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath + _queryString)
        {
            Content = new StringContent(_validationEvent, Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"validationResponse\":\"512d38b6-c7b8-40c8-89fe-f46f9e9622b6\"", responseBody);
    }

    private HttpClient GetTestClient()
    {
        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            var sendingServiceMock = new Mock<ISendingService>();

            builder.ConfigureTestServices(services =>
            {
                services.Configure<EmailDeliveryReportSettings>(opts =>
                {
                    opts.AccessKey = "accesskey";
                });
                services.AddSingleton(sendingServiceMock.Object);
            });
        }).CreateClient();

        return client;
    }
}
