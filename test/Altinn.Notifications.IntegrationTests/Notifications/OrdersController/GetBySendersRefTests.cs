using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Core.Models.Orders;
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

namespace Altinn.Notifications.IntegrationTests.Notifications.OrdersController;

public class GetBySendersRefTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.OrdersController>>, IDisposable
{
    private const string _basePath = "/notifications/api/v1/orders";

    private readonly IntegrationTestWebApplicationFactory<Controllers.OrdersController> _factory;

    private readonly string _sendersRefBase = $"ref-{Guid.NewGuid()}";

    public GetBySendersRefTests(IntegrationTestWebApplicationFactory<Controllers.OrdersController> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetBySendersRef_NoMatchInDb_ReturnsOK_EmptyList()
    {
        // Arrange
        string sendersReference = $"{_sendersRefBase}-{Guid.NewGuid()}";

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        string uri = $"{_basePath}?sendersReference={sendersReference}";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, uri);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string resString = await response.Content.ReadAsStringAsync();
        NotificationOrderListExt actual = JsonSerializer.Deserialize<NotificationOrderListExt>(resString, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } })!;

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, actual!.Count);
        Assert.Empty(actual.Orders);
    }

    [Fact]
    public async Task GetBySendersRef_SingleMatchInDb_ReturnsOk_SingleElementInlList()
    {
        // Arrange
        string sendersReference = $"{_sendersRefBase}-{Guid.NewGuid()}";
        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithOrder(sendersReference: sendersReference);

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        string uri = $"{_basePath}?sendersReference={sendersReference}";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, uri);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string resString = await response.Content.ReadAsStringAsync();
        NotificationOrderListExt actual = JsonSerializer.Deserialize<NotificationOrderListExt>(resString, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } })!;

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, actual.Count);
        Assert.Single(actual.Orders);
        Assert.Equal(persistedOrder.Id.ToString(), actual.Orders[0].Id);
        Assert.Equal(sendersReference, actual.Orders[0].SendersReference);
    }

    [Fact]
    public async Task GetBySendersRef_MultipleMatchInDb_ReturnsOk_MultipleElementInlList()
    {
        // Arrange
        string sendersReference = $"{_sendersRefBase}-{Guid.NewGuid()}";
        await PostgreUtil.PopulateDBWithOrder(sendersReference: sendersReference);
        await PostgreUtil.PopulateDBWithOrder(sendersReference: sendersReference);

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        string uri = $"{_basePath}?sendersReference={sendersReference}";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, uri);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string resString = await response.Content.ReadAsStringAsync();
        NotificationOrderListExt actual = JsonSerializer.Deserialize<NotificationOrderListExt>(resString, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } })!;

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, actual.Count);
        Assert.NotNull(actual.Orders[0]);
        Assert.DoesNotContain(actual.Orders, o => o.SendersReference != sendersReference);
        Assert.DoesNotContain(actual.Orders, o => o.Creator != "ttd");
    }

    public async void Dispose()
    {
        await Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual async Task Dispose(bool disposing)
    {
        string sql = $"delete from notifications.orders where sendersreference like '{_sendersRefBase}%'";
        await PostgreUtil.RunSql(sql);
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
