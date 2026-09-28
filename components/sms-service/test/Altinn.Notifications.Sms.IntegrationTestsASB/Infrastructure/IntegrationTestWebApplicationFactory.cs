using System.Collections.Concurrent;
using System.Reflection;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Altinn.Notifications.Sms.Core.Dependencies;
using Altinn.Notifications.Sms.Core.Sending;
using Altinn.Notifications.Sms.Integrations.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Altinn.Notifications.Sms.IntegrationTestsASB.Infrastructure;

/// <summary>
/// WebApplicationFactory for SMS service ASB integration tests.
/// Boots the real Program.cs with test-specific configuration and service overrides.
/// </summary>
public class IntegrationTestWebApplicationFactory(IntegrationTestContainersFixture fixture)
    : IntegrationTestWebApplicationFactoryBase<Program, IntegrationTestWebApplicationFactory>(fixture)
{
    private readonly Dictionary<Type, object> _installedServices = [];
    private readonly ConcurrentDictionary<Type, object> _defaultResolvedServices = new();

    /// <summary>
    /// Gets the Wolverine settings loaded from configuration.
    /// </summary>
    public WolverineSettings? WolverineSettings { get; private set; }

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

    /// <inheritdoc/>
    protected override Dictionary<string, string?> GetFixtureConfigOverrides() => new()
    {
        ["WolverineSettings:ServiceBusConnectionString"] = Fixture.ServiceBusConnectionString,
    };

    /// <inheritdoc/>
    protected override void ConfigureComponentServices(IConfiguration configuration, IServiceCollection services)
    {
        WolverineSettings = configuration.GetSection(nameof(WolverineSettings)).Get<WolverineSettings>()
            ?? throw new InvalidOperationException(
                "Missing WolverineSettings configuration for ASB integration tests.");

        Console.WriteLine($"[SmsFactory] ServiceBus connection: {Truncate(Fixture.ServiceBusConnectionString, 50)}...");

        RegisterDelegatedService<ISendingService>(services);
        RegisterDelegatedService<ISmsClient>(services);
    }

    /// <inheritdoc/>
    protected override async Task DrainQueuesAsync()
    {
        if (WolverineSettings == null)
        {
            return;
        }

        string[] allQueues =
        [
            WolverineSettings.SmsDeliveryReportQueueName,
            WolverineSettings.SmsSendResultQueueName,
            WolverineSettings.SendSmsQueueName
        ];

        await DrainMainQueuesAsync(Fixture.ServiceBusConnectionString, allQueues);
        await DrainDeadLetterQueuesAsync(Fixture.ServiceBusConnectionString, allQueues);
    }

    /// <summary>
    /// Registers an interface as a delegated service that can be overridden at runtime per test.
    /// </summary>
    /// <typeparam name="TService">Service contract type.</typeparam>
    /// <param name="services">Service collection to mutate.</param>
    private void RegisterDelegatedService<TService>(IServiceCollection services)
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
                if (_installedServices.TryGetValue(serviceType, out var service))
                {
                    return (TService)service;
                }

                var resolved = _defaultResolvedServices.GetOrAdd(serviceType, _ =>
                    ResolveServiceFromDescriptor(serviceProvider, fallbackDescriptor, serviceType));
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

            var target = _resolver();
            return targetMethod.Invoke(target, args);
        }
    }
}
