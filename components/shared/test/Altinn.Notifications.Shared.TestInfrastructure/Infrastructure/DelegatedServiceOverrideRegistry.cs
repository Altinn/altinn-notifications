using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

/// <summary>
/// Provides reusable registration and runtime override support for delegated test services.
/// </summary>
public class DelegatedServiceOverrideRegistry
{
    private readonly ConcurrentDictionary<Type, object> _installedServices = new();
    private readonly ConcurrentDictionary<Type, object> _defaultResolvedServices = new();

    /// <summary>
    /// Installs a service override for the specified contract type.
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
    /// Clears all installed service overrides.
    /// </summary>
    public void ResetInstalledMocks()
    {
        _installedServices.Clear();
    }

    /// <summary>
    /// Replaces a service registration with a delegating proxy that resolves to either an installed override or a default implementation.
    /// </summary>
    /// <typeparam name="TService">Service contract type.</typeparam>
    /// <param name="services">Service collection to mutate.</param>
    /// <param name="fallbackFactory">Optional fallback factory when no default descriptor exists.</param>
    public void RegisterDelegatedService<TService>(
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
                if (_installedServices.TryGetValue(serviceType, out var service))
                {
                    return (TService)service;
                }

                if (fallbackFactory != null)
                {
                    return fallbackFactory(serviceProvider);
                }

                if (lifetime == ServiceLifetime.Singleton)
                {
                    var resolvedSingleton = _defaultResolvedServices.GetOrAdd(serviceType, _ =>
                        ResolveServiceFromDescriptor(serviceProvider, fallbackDescriptor, serviceType));
                    return (TService)resolvedSingleton;
                }

                var resolved = ResolveServiceFromDescriptor(serviceProvider, fallbackDescriptor, serviceType);
                return (TService)resolved;
            }),
            lifetime));
    }

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

    private class InterfaceDispatchProxy<TService> : DispatchProxy
        where TService : class
    {
        private Func<TService>? _resolver;

        public static TService Create(Func<TService> resolver)
        {
            var proxy = DispatchProxy.Create<TService, InterfaceDispatchProxy<TService>>();
            ((InterfaceDispatchProxy<TService>)(object)proxy)._resolver = resolver;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (_resolver == null)
            {
                throw new InvalidOperationException($"{nameof(InterfaceDispatchProxy<TService>)} is not initialized.");
            }

            if (targetMethod == null)
            {
                throw new InvalidOperationException("Proxy invocation missing target method.");
            }

            try
            {
                var target = _resolver();
                return targetMethod.Invoke(target, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
