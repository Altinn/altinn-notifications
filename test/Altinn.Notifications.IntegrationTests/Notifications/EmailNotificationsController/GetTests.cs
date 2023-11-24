﻿using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;
using Altinn.Notifications.Tests.Notifications.Utils;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.EmailNotificationsController
{
    public class GetTests : IClassFixture<IntegrationTestWebApplicationFactory<Controllers.EmailNotificationsController>>, IAsyncLifetime
    {
        private readonly string _basePath;
        private readonly IntegrationTestWebApplicationFactory<Controllers.EmailNotificationsController> _factory;
        private readonly Task _initializeTask;
        private Guid _orderId;
        private Guid _notificationId;

        public GetTests(IntegrationTestWebApplicationFactory<Controllers.EmailNotificationsController> factory)
        {
            _basePath = $"/notifications/api/v1/orders";
            _factory = factory;
            _initializeTask = InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            (NotificationOrder persistedOrder, EmailNotification persistedNotification) = await PostgreUtil.PopulateDBWithOrderAndEmailNotification();
            _orderId = persistedOrder.Id;
            _notificationId = persistedNotification.Id;
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            string sql = $"delete from notifications.orders where alternateid= '{_orderId}'";

            await PostgreUtil.RunSql(sql);
        }

        [Fact]
        public async Task Get_NonExistingOrder_NotFound()
        {
            // Arrange
            string uri = $"{_basePath}/{Guid.NewGuid()}/notifications/email";

            HttpClient client = GetTestClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

            HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, uri);

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Get_OrderIdForAnotherCreator_NotFound()
        {
            // Arrange
            await _initializeTask;

            string uri = $"{_basePath}/{_orderId}/notifications/email";

            HttpClient client = GetTestClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("nav", scope: "altinn:serviceowner/notifications.create"));

            HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, uri);

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Get_ValidOrderId_Ok()
        {
            // Arrange
            await _initializeTask;

            string uri = $"{_basePath}/{_orderId}/notifications/email";

            HttpClient client = GetTestClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PrincipalUtil.GetOrgToken("ttd", scope: "altinn:serviceowner/notifications.create"));

            HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, uri);

            // Act
            HttpResponseMessage response = await client.SendAsync(httpRequestMessage);
            string responseString = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            EmailNotificationSummaryExt? summary = JsonSerializer.Deserialize<EmailNotificationSummaryExt>(responseString);
            Assert.True(summary?.Notifications.Count > 0);
            Assert.Equal(_orderId, summary?.OrderId);
            Assert.Equal(_notificationId, summary?.Notifications[0].Id);
            Assert.Equal(1, summary?.Generated);
            Assert.Equal(0, summary?.Succeeded);
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
}