using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Models;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;
using Altinn.Notifications.Tests.Notifications.Utils;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.EmailNotificationsOrderController;

public class PostTests : IClassFixture<IntegrationTestWebApplicationFactory<EmailNotificationOrdersController>>, IDisposable
{
    private const string _basePath = "/notifications/api/v1/orders/email";

    private readonly IntegrationTestWebApplicationFactory<EmailNotificationOrdersController> _factory;

    private readonly string _serializedOrderRequestExt;
    private readonly string _serializedOrderRequestWithoutSendersRefExt;

    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";

    public PostTests(IntegrationTestWebApplicationFactory<EmailNotificationOrdersController> factory)
    {
        _factory = factory;
        EmailNotificationOrderRequestExt orderRequestExt = new()
        {
            Body = "email-body",
            ContentType = EmailContentType.Html,
            FromAddress = "sender@domain.com",
            Recipients = new List<RecipientExt>()
            {
                new RecipientExt
                {
                    EmailAddress = "recipient1@domain.com"
                },
                new RecipientExt
                {
                    EmailAddress = "recipient2@domain.com"
                }
            },
            SendersReference = _sendersRef,
            RequestedSendTime = DateTime.UtcNow,
            Subject = "email-subject"
        };

        _serializedOrderRequestExt = orderRequestExt.Serialize();
        orderRequestExt.SendersReference = null;

        _serializedOrderRequestWithoutSendersRefExt = orderRequestExt.Serialize();
    }

    [Fact]
    public async Task Post_ServiceReturnsOrderWIthId_Accepted()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(_serializedOrderRequestExt, Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string orderId = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Guid.Parse(orderId);
        Assert.Equal("http://localhost:5090/notifications/api/v1/orders/" + orderId, response.Headers?.Location?.ToString());
    }

    [Fact]
    public async Task Post_OrderWithoutSendersRef_Accepted()
    {
        // Arrange
        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath)
        {
            Content = new StringContent(_serializedOrderRequestWithoutSendersRefExt, Encoding.UTF8, "application/json")
        };

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string orderId = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Guid.Parse(orderId);
        Assert.Equal("http://localhost:5090/notifications/api/v1/orders/" + orderId, response.Headers?.Location?.ToString());
    }

    public async void Dispose()
    {
        await Dispose(true);

        GC.SuppressFinalize(this);
    }

    protected virtual async Task Dispose(bool disposing)
    {
        string sql = $"delete from notifications.orders where sendersreference = '{_sendersRef}'";
        await PostgreUtil.RunSql(sql);
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
