using System.Collections.Concurrent;
using System.Reflection;

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
    private readonly Dictionary<Type, object> _installedServices = [];
    private readonly ConcurrentDictionary<Type, object> _defaultResolvedServices = new();

    /// <summary>
    /// Installs a service override that will be used by delegated test service resolution.
    /// </summary>
    /// <typeparam name="TService">Service contract type.</typeparam>
    /// <param name="service">Service instance to install.</param>
    public void InstallService<TService>(TService service)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(service);
        _installedServices[typeof(TService)] = service;
    }

    /// <summary>
    /// Clears all runtime-installed service overrides.
    /// </summary>
    public void ResetInstalledMocks()
    {
        _installedServices.Clear();
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
        var serviceType = typeof(TService);
        var existing = services.Where(s => s.ServiceType == serviceType).ToList();
        var lifetime = existing.LastOrDefault()?.Lifetime ?? ServiceLifetime.Singleton;
        var fallbackDescriptor = existing.LastOrDefault();

        foreach (var descriptor in existing)
        {
            services.Remove(descriptor);
        }

        services.Add(new ServiceDescriptor(
            serviceType,
            serviceProvider => InterfaceDispatchProxy<TService>.Create(() =>
            {
                if (_installedServices.TryGetValue(serviceType, out var installed))
                {
                    return (TService)installed;
                }

                if (fallbackFactory != null)
                {
                    return fallbackFactory(serviceProvider);
                }

                var resolved = _defaultResolvedServices.GetOrAdd(
                    serviceType,
                    _ => ResolveServiceFromDescriptor(serviceProvider, fallbackDescriptor, serviceType));
                return (TService)resolved;
            }),
            lifetime));
    }

    /// <summary>
    /// Resolves the default implementation from an existing service descriptor.
    /// </summary>
    /// <param name="serviceProvider">Service provider used for activation.</param>
    /// <param name="descriptor">Service descriptor to resolve from.</param>
    /// <param name="serviceType">Service contract type.</param>
    /// <returns>Resolved default service instance.</returns>
    private static object ResolveServiceFromDescriptor(
        IServiceProvider serviceProvider,
        ServiceDescriptor? descriptor,
        Type serviceType)
    {
        if (descriptor?.ImplementationInstance != null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor?.ImplementationFactory != null)
        {
            return descriptor.ImplementationFactory(serviceProvider);
        }

        if (descriptor?.ImplementationType != null)
        {
            return ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType);
        }

        return serviceProvider.GetRequiredService(serviceType);
    }

    /// <summary>
    /// Dispatch proxy that forwards interface calls to a dynamically resolved implementation.
    /// </summary>
    /// <typeparam name="TService">Service contract type.</typeparam>
    private class InterfaceDispatchProxy<TService> : DispatchProxy
        where TService : class
    {
        private Func<TService>? _resolver;

        /// <summary>
        /// Creates a proxy that resolves its target implementation at invocation time.
        /// </summary>
        /// <param name="resolver">Target resolver delegate.</param>
        /// <returns>Interface proxy instance.</returns>
        public static TService Create(Func<TService> resolver)
        {
            var proxy = Create<TService, InterfaceDispatchProxy<TService>>();
            ((InterfaceDispatchProxy<TService>)(object)proxy)._resolver = resolver;
            return proxy;
        }

        /// <summary>
        /// Invokes the target method on the currently resolved service instance.
        /// </summary>
        /// <param name="targetMethod">Target method metadata.</param>
        /// <param name="args">Invocation arguments.</param>
        /// <returns>Invocation result.</returns>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (_resolver == null)
            {
                throw new InvalidOperationException("Proxy resolver is not initialized.");
            }

            if (targetMethod == null)
            {
                throw new InvalidOperationException("Target method is null.");
            }

            try
            {
                var target = _resolver();
                return targetMethod.Invoke(target, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
