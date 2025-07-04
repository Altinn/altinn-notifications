﻿using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Core;
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

public class GetWithStatusById : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.OrdersController>>, IDisposable
{
    private const string _basePath = "/notifications/api/v1/orders";

    private readonly IntegrationTestWebApplicationFactory<Controllers.OrdersController> _factory;

    private readonly string _sendersRef = $"ref-{Guid.NewGuid()}";

    public GetWithStatusById(IntegrationTestWebApplicationFactory<Controllers.OrdersController> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetWithStatusById_NoMatchInDb_ReturnsNotFound()
    {
        // Arrange
        string uri = $"{_basePath}/{Guid.NewGuid()}/status";

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, uri);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWithStatusById_SingleMatchInDbAndOneEmail_ReturnsOk()
    {
        // Arrange
        (NotificationOrder persistedOrder, _) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification(sendersReference: _sendersRef, simulateCronJob: true, simulateConsumers: true);

        string refLinkBase = "http://localhost:5090/notifications/api/v1/orders";

        NotificationOrderWithStatusExt expected = new()
        {
            Id = persistedOrder.Id.ToString(),
            SendersReference = persistedOrder.SendersReference,
            Creator = "ttd",
            Created = persistedOrder.Created,
            NotificationChannel = (NotificationChannelExt)persistedOrder.NotificationChannel,
            RequestedSendTime = persistedOrder.RequestedSendTime,
            ProcessingStatus = new()
            {
                LastUpdate = persistedOrder.Created,
                Status = "Completed",
                StatusDescription = "Order processing is done. Notifications have been successfully generated."
            },
            NotificationsStatusSummary = new NotificationsStatusSummaryExt()
            {
                Email = new()
                {
                    Generated = 1,
                    Succeeded = 0,
                    Links = new()
                    {
                        Self = $"{refLinkBase}/{persistedOrder.Id}/notifications/email"
                    }
                }
            }
        };

        string uri = $"{_basePath}/{persistedOrder.Id}/status";

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, uri);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string resString = await response.Content.ReadAsStringAsync();
        NotificationOrderWithStatusExt? actual = JsonSerializer.Deserialize<NotificationOrderWithStatusExt>(resString, JsonSerializerOptionsProvider.Options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public async Task GetWithStatusById_SingleMatchInDbAndOneSms_ReturnsOk()
    {
        // Arrange
        (NotificationOrder persistedOrder, _) = await PostgreUtil.PopulateDBWithOrderAndSmsNotification(sendersReference: _sendersRef, simulateCronJob: true, simulateConsumers: true);

        string refLinkBase = "http://localhost:5090/notifications/api/v1/orders";

        NotificationOrderWithStatusExt expected = new()
        {
            Id = persistedOrder.Id.ToString(),
            SendersReference = persistedOrder.SendersReference,
            Creator = "ttd",
            Created = persistedOrder.Created,
            NotificationChannel = (NotificationChannelExt)persistedOrder.NotificationChannel,
            RequestedSendTime = persistedOrder.RequestedSendTime,
            ProcessingStatus = new()
            {
                Status = "Completed",
                LastUpdate = persistedOrder.Created,
                StatusDescription = "Order processing is done. Notifications have been successfully generated."
            },
            NotificationsStatusSummary = new NotificationsStatusSummaryExt()
            {
                Sms = new()
                {
                    Generated = 1,
                    Succeeded = 0,
                    Links = new()
                    {
                        Self = $"{refLinkBase}/{persistedOrder.Id}/notifications/sms"
                    }
                },
                Email = null
            }
        };

        string uri = $"{_basePath}/{persistedOrder.Id}/status";

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, uri);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string resString = await response.Content.ReadAsStringAsync();
        NotificationOrderWithStatusExt? actual = JsonSerializer.Deserialize<NotificationOrderWithStatusExt>(resString, JsonSerializerOptionsProvider.Options);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public async Task GetWithStatusById_SingleMatchInDb_ReturnsOk()
    {
        // Arrange
        NotificationOrder persistedOrder = await PostgreUtil.PopulateDBWithEmailOrder(sendersReference: _sendersRef);

        NotificationOrderWithStatusExt expected = new()
        {
            Id = persistedOrder.Id.ToString(),
            SendersReference = persistedOrder.SendersReference,
            Creator = "ttd",
            Created = persistedOrder.Created,
            NotificationChannel = (NotificationChannelExt)persistedOrder.NotificationChannel,
            RequestedSendTime = persistedOrder.RequestedSendTime,
            ProcessingStatus = new()
            {
                LastUpdate = persistedOrder.Created,
                Status = "Registered",
                StatusDescription = "Order has been registered and is awaiting requested send time before processing."
            }
        };

        string uri = $"{_basePath}/{persistedOrder.Id}/status";

        HttpClient client = GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, uri);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
        string resString = await response.Content.ReadAsStringAsync();
        NotificationOrderWithStatusExt? actual = JsonSerializer.Deserialize<NotificationOrderWithStatusExt>(resString, JsonSerializerOptionsProvider.Options);

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
