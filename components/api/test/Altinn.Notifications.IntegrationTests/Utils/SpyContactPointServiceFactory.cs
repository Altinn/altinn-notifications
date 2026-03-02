using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.IntegrationTests.Utils;

/// <summary>
/// Custom WebApplicationFactory that replaces IContactPointService with a spy implementation
/// to enable verification of OrderPhase values in integration tests.
/// </summary>
public class SpyContactPointServiceFactory : WebApplicationFactory<Program>
{
    public SpyContactPointService? SpyService { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.IntegrationTest.json")
            .Build();

        builder.ConfigureAppConfiguration((hostingContext, config) =>
        {
            config.AddConfiguration(configuration);

            string? uri = configuration["GeneralSettings:BaseUri"];
            ResourceLinkExtensions.Initialize(uri!);
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove all Kafka consumers - not needed for these tests
            var consumersToRemove = services
                .Where(s => s.ImplementationType?.IsAssignableTo(typeof(KafkaConsumerBase)) == true)
                .ToList();

            foreach (var descriptor in consumersToRemove)
            {
                services.Remove(descriptor);
            }

            // Remove the existing IContactPointService registration
            var contactPointDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IContactPointService));

            if (contactPointDescriptor != null)
            {
                services.Remove(contactPointDescriptor);
            }

            // Register the spy service
            services.AddSingleton<IContactPointService>(sp =>
            {


                SpyService = new SpyContactPointService();
                return SpyService;
            });

            // Auth mocks - configured once for all tests
            services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>();
            services.AddSingleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>();
        });
    }
}
