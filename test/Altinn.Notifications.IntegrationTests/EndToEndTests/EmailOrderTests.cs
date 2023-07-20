using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations.Consumers;
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
            Recipients = new List<RecipientExt>()
            {
                new RecipientExt
                {
                    EmailAddress ="recipient1@domain.com"
                },
                new RecipientExt
                {
                    EmailAddress ="recipient2@domain.com"
                }
            },
            SendersReference = "senders-reference",
            RequestedSendTime = DateTime.UtcNow,
            Subject = "email-subject"
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
        string orderId = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("https://platform.at22.altinn.cloud/notifications/api/v1/orders/" + orderId, response.Headers?.Location?.ToString());
        Guid.Parse(orderId);
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

                var descriptor = services.Single(s => s.ImplementationType == typeof(PastDueOrdersConsumer));
                services.Remove(descriptor);
            });
        }).CreateClient();

        return client;
    }
}