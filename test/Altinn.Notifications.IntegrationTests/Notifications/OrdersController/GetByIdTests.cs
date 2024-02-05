using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;
using Altinn.Notifications.Tests.Notifications.Utils;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.OrdersController;

public class GetByIdTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.OrdersController>>, IDisposable
{
    private const string _basePath = "/notifications/api/v1/orders";

    private readonly IntegrationTestWebApplicationFactory<Controllers.OrdersController> _factory;

    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";

    public GetByIdTests(IntegrationTestWebApplicationFactory<Controllers.OrdersController> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetById_NoMatchInDb_ReturnsNotFound()
    {
        // Arrange
        string uri = $"{_basePath}/{Guid.NewGuid()}";

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, uri);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEmailOrderById_SingleMatchInDb_ReturnsOk()
    {
        // Arrange
        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithEmailOrder(sendersReference: _sendersRef);

        // mapping to orderExt, but not using it directly to ensure mapping logic isn't affecting test result
        var mappedExtOrder = persistedOrder.MapToNotificationOrderExt();

        string refLinkBase = "http://localhost:5090/notifications/api/v1/orders";
        string id = persistedOrder.Id.ToString();

        NotificationOrderExt expected = new()
        {
            Id = persistedOrder.Id.ToString(),
            SendersReference = persistedOrder.SendersReference,
            Creator = "ttd",
            Created = persistedOrder.Created,
            Links = new()
            {
                Notifications = $"{refLinkBase}/{id}/notifications",
                Self = $"{refLinkBase}/{id}",
                Status = $"{refLinkBase}/{id}/status"
            },
            NotificationChannel = (NotificationChannelExt)persistedOrder.NotificationChannel,
            RequestedSendTime = persistedOrder.RequestedSendTime,
            Recipients = mappedExtOrder.Recipients,
            EmailTemplate = mappedExtOrder.EmailTemplate
        };

        string uri = $"{_basePath}/{persistedOrder.Id}";

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, uri);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string resString = await response.Content.ReadAsStringAsync();
        NotificationOrderExt? actual = JsonSerializer.Deserialize<NotificationOrderExt>(resString, JsonSerializerOptionsProvider.Options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public async Task GetSmsOrderById_SingleMatchInDb_ReturnsOk()
    {
        // Arrange
        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithSmsOrder(sendersReference: _sendersRef);

        // mapping to orderExt, but not using it directly to ensure mapping logic isn't affecting test result
        var mappedExtOrder = persistedOrder.MapToNotificationOrderExt();

        string refLinkBase = "http://localhost:5090/notifications/api/v1/orders";
        string id = persistedOrder.Id.ToString();

        NotificationOrderExt expected = new()
        {
            Id = persistedOrder.Id.ToString(),
            SendersReference = persistedOrder.SendersReference,
            Creator = "ttd",
            Created = persistedOrder.Created,
            Links = new()
            {
                Self = $"{refLinkBase}/{id}",
                Status = $"{refLinkBase}/{id}/status"
            },
            NotificationChannel = (NotificationChannelExt)persistedOrder.NotificationChannel,
            RequestedSendTime = persistedOrder.RequestedSendTime,
            Recipients = mappedExtOrder.Recipients,
            SmsTemplate = mappedExtOrder.SmsTemplate            
        };

        string uri = $"{_basePath}/{persistedOrder.Id}";

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, uri);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string resString = await response.Content.ReadAsStringAsync();
        NotificationOrderExt? actual = JsonSerializer.Deserialize<NotificationOrderExt>(resString, JsonSerializerOptionsProvider.Options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equivalent(expected, actual);
    }

    public async void Dispose()
    {
        await Dispose(true);

        GC.SuppressFinalize(this);
    }

    protected virtual async Task Dispose(bool disposing)
    {
        await PostgreUtil.DeleteOrderFromDb(_sendersRef);
    }

    private HttpClient GetTestClient()
    {
        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            IdentityModelEventSource.ShowPII = true;

            builder.ConfigureTestServices(services =>
            {
                // Set up mock authentication and authorization
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
            });
        }).CreateClient();

        return client;
    }
}
