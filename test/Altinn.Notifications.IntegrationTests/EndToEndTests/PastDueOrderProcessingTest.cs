﻿using System.Net;
using System.Net.Http.Headers;
using System.Text;

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

public class PastDueOrderProcessingTest : IClassFixture<IntegrationTestWebApplicationFactory<TriggerController>>
{
    private const string _basePath = "/notifications/api/v1/trigger/pastdueorders";

    private readonly IntegrationTestWebApplicationFactory<TriggerController> _factory;

    public PastDueOrderProcessingTest(IntegrationTestWebApplicationFactory<TriggerController> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_ServiceReturnsOrder_Accepted()
    {
        // Arrange
        HttpClient client = GetTestClient();

        HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, _basePath);

        // Act
        HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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