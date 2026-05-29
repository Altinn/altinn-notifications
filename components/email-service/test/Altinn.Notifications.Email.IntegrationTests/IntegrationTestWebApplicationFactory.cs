using Altinn.Notifications.Email.Core.Dependencies;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

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
        builder.ConfigureAppConfiguration((hostingContext, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=ZmFrZQ=="
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Strip all Wolverine services to prevent ASB connection attempts.
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

            services.AddSingleton(Mock.Of<IEmailSendResultDispatcher>());
            services.AddSingleton(Mock.Of<IEmailServiceRateLimitDispatcher>());
            services.AddSingleton(Mock.Of<IEmailStatusCheckDispatcher>());
        });
    }
}
