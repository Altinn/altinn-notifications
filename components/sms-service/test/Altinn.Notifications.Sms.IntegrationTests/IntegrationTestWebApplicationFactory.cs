using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Sms.Configuration;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Core.Status;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

namespace Altinn.Notifications.Sms.IntegrationTests;

public class IntegrationTestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup>
      where TStartup : class
{
    private readonly DelegatedServiceOverrideRegistry _delegatedOverrides = new();

    /// <summary>
    /// Installs a service override that will be used by delegated test service resolution.
    /// </summary>
    /// <typeparam name="TService">Service contract type.</typeparam>
    /// <param name="service">Service instance to install.</param>
    public void InstallService<TService>(TService service)
        where TService : class
    {
        _delegatedOverrides.InstallService(service);
    }

    /// <summary>
    /// Clears all runtime-installed service overrides.
    /// </summary>
    public void ResetInstalledMocks()
    {
        _delegatedOverrides.ResetInstalledMocks();
    }

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

            RegisterDelegatedService<ISendingService>(services);
            RegisterDelegatedService<IStatusService>(services);
            RegisterDelegatedService<ISmsSendResultDispatcher>(services, _ => Mock.Of<ISmsSendResultDispatcher>());
            RegisterDelegatedService<ISmsDeliveryReportPublisher>(services, _ => Mock.Of<ISmsDeliveryReportPublisher>());

            var existingSettings = services.SingleOrDefault(d => d.ServiceType == typeof(SmsDeliveryReportSettings));
            if (existingSettings != null)
            {
                services.Remove(existingSettings);
            }

            services.AddSingleton(new SmsDeliveryReportSettings
            {
                UserSettings = new UserSettings
                {
                    Username = "username",
                    Password = "password"
                }
            });
        });
    }

    /// <summary>
    /// Registers an interface as a delegated service that can be overridden at runtime per test.
    /// </summary>
    /// <typeparam name="TService">Service contract type.</typeparam>
    /// <param name="services">Service collection to mutate.</param>
    /// <param name="fallbackFactory">Optional fallback factory when no default descriptor exists.</param>
    private void RegisterDelegatedService<TService>(
        IServiceCollection services,
        Func<IServiceProvider, TService>? fallbackFactory = null)
        where TService : class
    {
        _delegatedOverrides.RegisterDelegatedService(services, fallbackFactory);
    }
}
