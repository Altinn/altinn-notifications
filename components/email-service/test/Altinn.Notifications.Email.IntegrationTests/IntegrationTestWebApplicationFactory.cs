using Altinn.Notifications.Integrations.Kafka.Consumers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace Altinn.Notifications.Email.IntegrationTests;

public class IntegrationTestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup>
      where TStartup : class
{
    /// <summary>
    /// Configures the web host for setting up configuration and test services.
    /// </summary>
    /// <param name="builder">The web host builder.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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
