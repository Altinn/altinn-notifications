using Altinn.Notifications.Sms.Integrations.Consumers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace Altinn.Notifications.Sms.IntegrationTests;

public class IntegrationTestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup>
      where TStartup : class
{
    /// <summary>
    /// ConfigureWebHost for setup of configuration and test services
    /// </summary>
    /// <param name="builder">IWebHostBuilder</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("WolverineSettings__EnableWolverine", "false");

        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.Single(s => s.ImplementationType == typeof(SendSmsQueueConsumer));
            services.Remove(descriptor);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Environment.SetEnvironmentVariable("WolverineSettings__EnableWolverine", null);
        }

        base.Dispose(disposing);
    }
}
