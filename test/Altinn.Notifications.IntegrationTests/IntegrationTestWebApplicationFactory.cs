using Altinn.Notifications.Extensions;
using Altinn.Notifications.Integrations.Kafka.Consumers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace Altinn.Notifications.Tests.EndToEndTests;

public class IntegrationTestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup>
      where TStartup : class
{
    /// <summary>
    /// ConfigureWebHost for setup of configuration and test services
    /// </summary>
    /// <param name="builder">IWebHostBuilder</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.IntegrationTest.json")
        .Build();

        builder.ConfigureAppConfiguration((hostingContext, config) =>
        {
            config.AddConfiguration(configuration);

            // overriding initialization of extension class with test settings
            string? uri = configuration["GeneralSettings:BaseUri"];
            ResourceLinkExtensions.Initialize(uri!);
        });

        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.Single(s => s.ImplementationType == typeof(PastDueOrdersConsumer));
            services.Remove(descriptor);

        });
    }
}