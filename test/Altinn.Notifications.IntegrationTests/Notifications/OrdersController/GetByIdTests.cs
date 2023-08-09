﻿using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Mappers;
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, uri);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_SingleMatchInDb_ReturnsOk()
    {
        // Arrange
        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithOrder(sendersReference: _sendersRef);

        // mapping to orderExt, but not using it directly to ensure mapping logic isn't affecting test result
        var mappedExtOrder = persistedOrder.MapToNotificationOrderExt();

        string refLinkBase = "https://platform.at22.altinn.cloud/notifications/api/v1/orders";
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
            NotificationChannel = persistedOrder.NotificationChannel,
            RequestedSendTime = persistedOrder.RequestedSendTime,
            Recipients = mappedExtOrder.Recipients,
            EmailTemplate = mappedExtOrder.EmailTemplate
        };

        string uri = $"{_basePath}/{persistedOrder.Id}";

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, uri);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string resString = await response.Content.ReadAsStringAsync();
        NotificationOrderExt? actual = JsonSerializer.Deserialize<NotificationOrderExt>(resString, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } });

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