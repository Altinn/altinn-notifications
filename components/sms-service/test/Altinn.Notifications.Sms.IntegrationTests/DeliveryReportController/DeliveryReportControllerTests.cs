using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Altinn.Notifications.Sms.Configuration;
using Altinn.Notifications.Sms.Core.Sending;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Moq;

namespace Altinn.Notifications.Sms.IntegrationTests.DeliveryReportController;

public class DeliveryReportControllerTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.DeliveryReportController>>
{
    private const string _basePath = "/notifications/sms/api/v1/reports";

    private readonly string _username = "username";
    private readonly string _password = "password";

    private readonly string _drMessageString = "<?xml version=\"1.0\"?><!DOCTYPE MSGLST SYSTEM \"pswincom_report_request.dtd\"><MSGLST><MSG><ID>1</ID><REF>984342374</REF><RCV>4512345678</RCV><STATE>DELIVRD</STATE><DELIVERYTIME>2006.02.23 15:23:23</DELIVERYTIME></MSG></MSGLST>";

    private readonly IntegrationTestWebApplicationFactory<Controllers.DeliveryReportController> _factory;

    public DeliveryReportControllerTests(IntegrationTestWebApplicationFactory<Controllers.DeliveryReportController> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_MissingBearerToken_Unauthorized()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidBearerToken_InvalidAuthorizationHeader_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath);
        httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"invalid")));

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidBearerToken_InvalidUserName_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath);
        httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"invalidusername:{_password}")));

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidBearerToken_InvalidDeliveryReport_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath);
        httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_username}:{_password}")));

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidBearerToken_ValidDeliveryReport_ReturnsOK()
    {
        // Arrange
        HttpClient client = GetTestClient();
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(_drMessageString, Encoding.UTF8, "application/xml")
        };

        httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_username}:{_password}")));

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private HttpClient GetTestClient()
    {
        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            var sendingServiceMock = new Mock<ISendingService>();

            builder.ConfigureTestServices(services =>
            {
                services.Configure<DeliveryReportSettings>(opts =>
                {
                    opts.UserSettings.Username = _username;
                    opts.UserSettings.Password = _password;
                });

                services.AddSingleton(sendingServiceMock.Object);
            });
        }).CreateClient();

        return client;
    }
}
