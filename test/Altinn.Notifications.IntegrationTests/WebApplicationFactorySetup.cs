using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Integrations.Clients;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace Altinn.Notifications.IntegrationTests;

public class WebApplicationFactorySetup<T>
     where T : class
{
    private readonly IntegrationTestWebApplicationFactory<T> _webApplicationFactory;

    public WebApplicationFactorySetup(IntegrationTestWebApplicationFactory<T> webApplicationFactory)
    {
        _webApplicationFactory = webApplicationFactory;
    }

    public Mock<ILogger<ProfileClient>> ProfileClientLogger { get; set; } = new();

    public AltinnServiceSettings AltinnServiceSettingsOptions { get; set; } = new();

    public HttpMessageHandler SblBridgeHttpMessageHandler { get; set; } = new DelegatingHandlerStub();

    public HttpClient GetTestServerClient()
    {
        MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions());

        AltinnServiceSettings altinnServiceSettings = new()
        {
            ApiProfileEndpoint = "https://at22.altinn.cloud/sblbridge/profile/api/"
        };

        return _webApplicationFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
                services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
                services.AddSingleton<IMemoryCache>(memoryCache);

                // Using the real/actual implementation of IUserProfiles, but with a mocked message handler.
                // Haven't found any other ways of injecting a mocked message handler to simulate SBL Bridge.
                services.AddSingleton<IProfileClient>(
                    new ProfileClient(
                        new HttpClient(SblBridgeHttpMessageHandler),
                        altinnServiceSettings));
            });
        }).CreateClient();
    }
}
