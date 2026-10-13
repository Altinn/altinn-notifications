using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Altinn.Common.AccessToken.Services;
using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Tests.Notifications.Mocks.Authentication;
using AltinnCore.Authentication.JwtCookie;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Moq;

namespace Altinn.Notifications.IntegrationTests;

public class IntegrationTestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup>
      where TStartup : class
{
    private readonly ConcurrentDictionary<Type, object> _installedServices = new();
    private readonly ConcurrentDictionary<Type, object> _defaultResolvedServices = new();
    private HttpClient? _sharedClient;

    public HttpClient SharedClient
    {
        get
        {
            _sharedClient ??= CreateClient();
            _sharedClient.DefaultRequestHeaders.Clear();
            return _sharedClient;
        }
    }

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
    /// Clears runtime-installed service overrides.
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
        IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.IntegrationTest.json")
                .Build();

        builder.ConfigureAppConfiguration((hostingContext, config) =>
        {
            config.AddConfiguration(configuration);
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WolverineSettings:ServiceBusConnectionString"] = "Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=ZmFrZQ=="
            });

            string? uri = configuration["GeneralSettings:BaseUri"];
            if (!string.IsNullOrEmpty(uri))
            {
                ResourceLinkExtensions.Initialize(uri);
            }
        });

        builder.ConfigureTestServices(services =>
        {
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

            services.Replace(ServiceDescriptor.Singleton(Mock.Of<ISendSmsPublisher>()));
            services.Replace(ServiceDescriptor.Singleton(Mock.Of<IEmailCommandPublisher>()));
            services.Replace(ServiceDescriptor.Singleton(Mock.Of<IComposedEmailCommandPublisher>()));

            RegisterDelegatedService<IStatusFeedService>(services);
            RegisterDelegatedService<ISmsNotificationService>(services);
            RegisterDelegatedService<IOrderProcessingService>(services);
            RegisterDelegatedService<IEmailNotificationService>(services);
            RegisterDelegatedService<INotificationScheduleService>(services);
            RegisterDelegatedService<IOrderRequestService>(services);
            RegisterDelegatedService<ISmsNotificationSummaryService>(services);
            RegisterDelegatedService<IEmailNotificationSummaryService>(services);
            RegisterDelegatedService<INotificationDeliveryManifestService>(services);
            RegisterDelegatedService<IGetOrderService>(services);
            RegisterDelegatedService<ICancelOrderService>(services);
            RegisterDelegatedService<INotificationLogService>(services);
            RegisterDelegatedService<IMetricsService>(services);
            RegisterDelegatedService<IDashboardService>(services);
            RegisterDelegatedService<IDateTimeService>(services);
            RegisterDelegatedService<IInstantOrderRequestService>(services);
            RegisterDelegatedService<IComposedEmailOrderRequestService>(services);
            RegisterDelegatedService<IValidator<SmsNotificationOrderRequestExt>>(services);
            RegisterDelegatedService<IValidator<EmailNotificationOrderRequestExt>>(services);
            RegisterDelegatedService<IValidator<InstantNotificationOrderRequestExt>>(services);
            RegisterDelegatedService<IValidator<InstantSmsNotificationOrderRequestExt>>(services);
            RegisterDelegatedService<IValidator<InstantEmailNotificationOrderRequestExt>>(services);
            RegisterDelegatedService<IDialogportenClient>(services);

            services.Replace(ServiceDescriptor.Singleton<IPostConfigureOptions<JwtCookieOptions>, JwtCookiePostConfigureOptionsStub>());
            services.Replace(ServiceDescriptor.Singleton<IPublicSigningKeyProvider, PublicSigningKeyProviderMock>());

            services.Configure<GeneralSettings>(opts =>
            {
                opts.BaseUri = "http://localhost:5090";
            });
        });
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

    /// <summary>
    /// Resolves the default implementation from an existing service descriptor.
    /// </summary>
    /// <param name="serviceProvider">Service provider used for activation.</param>
    /// <param name="descriptor">Service descriptor to resolve from.</param>
    /// <param name="serviceType">Service contract type.</param>
    /// <returns>Resolved default service instance.</returns>
    private static object ResolveServiceFromDescriptor(IServiceProvider serviceProvider, ServiceDescriptor? descriptor, Type serviceType)
    {
        if (descriptor == null)
        {
            throw new InvalidOperationException($"No registration exists for delegated service type {serviceType.FullName} and no test override is installed.");
        }

        if (descriptor.ImplementationInstance != null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory != null)
        {
            return descriptor.ImplementationFactory(serviceProvider)!;
        }

        if (descriptor.ImplementationType != null)
        {
            return ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType);
        }

        throw new InvalidOperationException($"Unable to resolve default implementation for {serviceType.FullName}.");
    }

    /// <summary>
    /// Dispatch proxy that forwards interface calls to a dynamically resolved implementation.
    /// </summary>
    /// <typeparam name="TService">Service contract type.</typeparam>
    private class InterfaceDispatchProxy<TService> : DispatchProxy
        where TService : class
    {
        private Func<TService>? _targetAccessor;

        /// <summary>
        /// Creates a proxy that resolves its target implementation at invocation time.
        /// </summary>
        /// <param name="targetAccessor">Target resolver delegate.</param>
        /// <returns>Interface proxy instance.</returns>
        public static TService Create(Func<TService> targetAccessor)
        {
            var proxy = DispatchProxy.Create<TService, InterfaceDispatchProxy<TService>>();
            ((InterfaceDispatchProxy<TService>)(object)proxy)._targetAccessor = targetAccessor;
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
            if (_targetAccessor == null)
            {
                throw new InvalidOperationException($"{nameof(InterfaceDispatchProxy<TService>)} is not initialized.");
            }

            if (targetMethod == null)
            {
                throw new InvalidOperationException("Proxy invocation missing target method.");
            }

            try
            {
                var target = _targetAccessor();
                return targetMethod.Invoke(target, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw; // unreachable, satisfies compiler
            }
        }
    }
}
