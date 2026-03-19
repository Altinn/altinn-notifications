using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.IntegrationTests;

public class IntegrationTestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup>
      where TStartup : class
{
    /// <summary>
    /// ConfigureWebHost for setup of configuration and test services
    /// </summary>
    /// <param name="builder">IWebHostBuilder</param>
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
                { "WolverineSettings:EnableWolverine", "false" }
            });

            // overriding initialization of extension class with test settings
            string? uri = configuration["GeneralSettings:BaseUri"];
            if (!string.IsNullOrEmpty(uri))
            {
                ResourceLinkExtensions.Initialize(uri);
            }
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove all Kafka consumers - controller tests don't need them
            var consumersToRemove = services
                .Where(s => s.ImplementationType?.IsAssignableTo(typeof(KafkaConsumerBase)) == true)
                .ToList();

            foreach (var descriptor in consumersToRemove)
            {
                services.Remove(descriptor);
            }

            // Replace scoped IEmailCommandPublisher (registered by Wolverine) with a singleton
            // no-op to avoid the scoped-from-singleton lifetime validation error.
            var emailPublisherDescriptors = services
                .Where(s => s.ServiceType == typeof(IEmailCommandPublisher))
                .ToList();

            foreach (var descriptor in emailPublisherDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IEmailCommandPublisher, SpyEmailCommandPublisher>();
        });
    }
}
