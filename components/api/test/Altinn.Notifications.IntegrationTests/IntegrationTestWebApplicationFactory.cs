using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Integrations.Kafka.Consumers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Moq;

namespace Altinn.Notifications.IntegrationTests;

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
        // No ServiceBusConnectionString is configured in the test environment, so Wolverine must not attempt to connect.
        Environment.SetEnvironmentVariable("WolverineSettings__EnableWolverine", "false");

        IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.IntegrationTest.json")
        .Build();

        builder.ConfigureAppConfiguration((hostingContext, config) =>
        {
            config.AddConfiguration(configuration);

            // overriding initialization of the extension class with test settings
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
