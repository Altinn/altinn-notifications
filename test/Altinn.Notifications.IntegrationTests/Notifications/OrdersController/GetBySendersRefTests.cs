using System.Net.Http.Headers;
using System.Net;

using Altinn.Notifications.Tests.EndToEndTests;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;
using Altinn.Notifications.Tests.Notifications.Utils;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Xunit;
using Altinn.Notifications.Models;
using System.Text.Json.Serialization;
using System.Text.Json;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.IntegrationTests.Notifications.OrdersController;


public class GetBySendersRefTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.OrdersController>>
{
    private const string _basePath = "/notifications/api/v1/orders";

    private readonly IntegrationTestWebApplicationFactory<Controllers.OrdersController> _factory;

    public GetBySendersRefTests(IntegrationTestWebApplicationFactory<Controllers.OrdersController> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetBySendersRef_NoMatchInDb_ReturnsOK_EmptyList()
    {
        // Arrange
        string uri = $"{_basePath}?sendersReference={Guid.NewGuid()}";

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

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
        string sendersReference = $"ref-{Guid.NewGuid()}";
        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithOrder(sendersReference: sendersReference);

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

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
        Assert.Equal(persistedOrder.Id.ToString(), actual.Orders.First().Id);
        Assert.Equal(sendersReference, actual.Orders.First().SendersReference);
    }

    [Fact]
    public async Task GetBySendersRef_MultipleMatchInDb_ReturnsOk_MultipleElementInlList()
    {
        // Arrange
        string sendersReference = $"ref-{Guid.NewGuid()}";
        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithOrder(sendersReference: sendersReference);
        await PostgreUtil.PopulateDBWithOrder(sendersReference: sendersReference);

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

        string uri = $"{_basePath}?sendersReference={sendersReference}";
        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, uri);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string resString = await response.Content.ReadAsStringAsync();
        NotificationOrderListExt actual = JsonSerializer.Deserialize<NotificationOrderListExt>(resString, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } })!;

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, actual.Count);
        Assert.NotNull(actual.Orders.First());
        Assert.DoesNotContain(actual.Orders, o => o.SendersReference != sendersReference);
        Assert.DoesNotContain(actual.Orders, o => o.Creator != "ttd");
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
