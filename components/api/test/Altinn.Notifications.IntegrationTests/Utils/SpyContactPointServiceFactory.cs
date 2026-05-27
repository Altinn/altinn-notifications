using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Core.Integrations;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Moq;

namespace Altinn.Notifications.IntegrationTests.Utils;

/// <summary>
/// Custom WebApplicationFactory that replaces IContactPointService with a spy implementation
/// to enable verification of OrderLifecycleStage values in integration tests.
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
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=ZmFrZQ=="
            });

            string? uri = configuration["GeneralSettings:BaseUri"];
            if (!string.IsNullOrEmpty(uri))
            {
                ResourceLinkExtensions.Initialize(uri);
            }
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove Kafka consumers — they are hosted services that would try to connect to a broker on startup.
            var consumersToRemove = services
                .Where(s => s.ImplementationType?.IsAssignableTo(typeof(KafkaConsumerBase)) == true)
                .ToList();

            foreach (var descriptor in consumersToRemove)
            {
                services.Remove(descriptor);
            }

            // Replace the Kafka producer with a no-op mock — keeps IKafkaProducer resolvable without a real broker.
            services.Replace(ServiceDescriptor.Singleton(Mock.Of<IKafkaProducer>()));

            var wolverineServices = services
                .Where(s =>
                    s.ServiceType.Assembly.GetName().Name?.StartsWith("Wolverine") == true ||
                    s.ImplementationType?.Assembly.GetName().Name?.StartsWith("Wolverine") == true ||
                    s.ImplementationFactory?.Method.DeclaringType?.Assembly.GetName().Name?.StartsWith("Wolverine") == true)
                .ToList();

            foreach (var descriptor in wolverineServices)
            {
                services.Remove(descriptor);
            }

            services.Replace(ServiceDescriptor.Singleton(Mock.Of<ISendSmsPublisher>()));
            services.Replace(ServiceDescriptor.Singleton(Mock.Of<IEmailCommandPublisher>()));
            services.Replace(ServiceDescriptor.Singleton(Mock.Of<IPastDueOrderPublisher>()));

            // Remove the existing IContactPointService registration
            var contactPointDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IContactPointService));

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
