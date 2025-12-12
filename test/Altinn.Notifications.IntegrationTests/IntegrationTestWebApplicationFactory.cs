using Altinn.Notifications.Extensions;
using Altinn.Notifications.Integrations.Kafka.Consumers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

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
                .AddJsonFile(path: "appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile(path: "appsettings.IntegrationTest.json", optional: false, reloadOnChange: false)
        .Build();

        // Disable file watching to prevent inotify limit issues in tests
        builder.UseSetting("hostBuilder:reloadConfigOnChange", "false");

        builder.ConfigureAppConfiguration((hostingContext, config) =>
        {
            config.AddConfiguration(configuration);

            // overriding initialization of extension class with test settings
            string? uri = configuration["GeneralSettings:BaseUri"];
            ResourceLinkExtensions.Initialize(uri!);
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
        });
    }
}
