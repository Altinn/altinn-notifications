using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Integrations.Consumers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;

namespace Altinn.Notifications.Sms.IntegrationTests;

public class IntegrationTestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup>
      where TStartup : class
{
    private readonly string? _originalEnableWolverine = Environment.GetEnvironmentVariable("WolverineSettings__EnableWolverine");

    /// <summary>
    /// Configures the web host for setting up configuration and test services.
    /// </summary>
    /// <param name="builder">The web host builder.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Disable Wolverine/ASB globally: no ServiceBusConnectionString is configured in the
        // test environment, so Wolverine must not attempt to connect.
        Environment.SetEnvironmentVariable("WolverineSettings__EnableWolverine", "false");

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

            // Replace the Kafka producer with a mock to prevent broker connection attempts
            services.Replace(ServiceDescriptor.Singleton(Mock.Of<ICommonProducer>()));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Environment.SetEnvironmentVariable("WolverineSettings__EnableWolverine", _originalEnableWolverine);
        }

        base.Dispose(disposing);
    }
}
