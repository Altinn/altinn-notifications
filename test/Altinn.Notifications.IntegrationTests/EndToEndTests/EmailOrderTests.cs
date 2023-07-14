using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Models;
using Altinn.Notifications.Tests.EndToEndTests;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;
using Altinn.Notifications.Tests.Notifications.Utils;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.RepositoryTests;

public class EmailOrderTests : IClassFixture<IntegrationTestWebApplicationFactory<EmailNotificationOrdersController>>
{
    private const string _basePath = "/notifications/api/v1/orders/email";

    private readonly IntegrationTestWebApplicationFactory<EmailNotificationOrdersController> _factory;

    private readonly EmailNotificationOrderRequestExt _orderRequestExt;

    public EmailOrderTests(IntegrationTestWebApplicationFactory<EmailNotificationOrdersController> factory)
    {
        _factory = factory;
        _orderRequestExt = new()
        {
            Body = "email-body",
            ContentType = EmailContentType.Html,
            FromAddress = "sender@domain.com",
            Recipients = null,
            SendersReference = "senders-reference",
            SendTime = DateTime.UtcNow,
            Subject = "email-subject",
            ToAddresses = new List<string>() { "recipient1@domain.com", "recipient2@domain.com" }
        };
    }

    [Fact]
    public async Task Post_ServiceReturnsOrder_Accepted()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(_orderRequestExt.Serialize(), Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string json = await response.Content.ReadAsStringAsync();
        JsonSerializer.Deserialize<NotificationOrderExt>(json, new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        });

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private HttpClient GetTestClient()
    {
        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                // Set up mock authentication so that not well known endpoint is used
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
            });
        }).CreateClient();

        return client;
    }
}