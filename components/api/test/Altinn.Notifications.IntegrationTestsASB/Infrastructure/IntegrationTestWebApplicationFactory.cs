using System.Collections.Concurrent;
using System.Reflection;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Integrations.Configuration;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Altinn.Notifications.IntegrationTestsASB.Infrastructure;

/// <summary>
/// WebApplicationFactory for API ASB integration tests.
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
        ["PostgreSQLSettings:ConnectionString"] = Fixture.PostgresConnectionString,
        ["PostgreSQLSettings:AdminConnectionString"] = Fixture.PostgresConnectionString,
        ["PostgreSQLSettings:NotificationsDbAdminPwd"] = string.Empty,
        ["PostgreSQLSettings:NotificationsDbPwd"] = string.Empty,
        ["PostgreSQLSettings:MigrationScriptPath"] = FindMigrationPath()
    };

    /// <inheritdoc/>
    protected override void ConfigureComponentServices(IConfiguration configuration, IServiceCollection services)
    {
        WolverineSettings = configuration.GetSection(nameof(WolverineSettings)).Get<WolverineSettings>()
            ?? throw new InvalidOperationException("WolverineSettings not found in configuration");

        Console.WriteLine($"[Factory] Loaded WolverineSettings - ServiceBus connection: {Truncate(WolverineSettings.ServiceBusConnectionString, 50)}...");
        Console.WriteLine($"[Factory] Postgres connection: {Truncate(Fixture.PostgresConnectionString, 50)}...");

        string? uri = configuration["GeneralSettings:BaseUri"];
        if (!string.IsNullOrEmpty(uri))
        {
            ResourceLinkExtensions.Initialize(uri);
        }

        services.Replace(ServiceDescriptor.Singleton(sp =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(Fixture.PostgresConnectionString);
            dataSourceBuilder.EnableParameterLogging(true);
            dataSourceBuilder.EnableDynamicJson();
            return dataSourceBuilder.Build();
        }));

        RegisterDelegatedService<IEmailNotificationService>(services);
        RegisterDelegatedService<ISmsNotificationService>(services);
        RegisterDelegatedService<IAltinnServiceUpdateService>(services);
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
            WolverineSettings.SendSmsQueueName,
            WolverineSettings.EmailSendQueueName,
            WolverineSettings.PastDueOrdersQueueName,
            WolverineSettings.SmsSendResultQueueName,
            WolverineSettings.EmailSendResultQueueName,
            WolverineSettings.ComposedEmailSendQueueName,
            WolverineSettings.SmsDeliveryReportQueueName,
            WolverineSettings.EmailDeliveryReportQueueName,
            WolverineSettings.EmailServiceRateLimitQueueName
        ];

        await DrainMainQueuesAsync(Fixture.ServiceBusConnectionString, allQueues);
        await DrainDeadLetterQueuesAsync(Fixture.ServiceBusConnectionString, allQueues);
    }

    /// <inheritdoc/>
    protected override async Task CleanupAsync()
    {
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(Fixture.PostgresConnectionString);
            await using var cmd = dataSource.CreateCommand(
                "TRUNCATE notifications.orderschain, notifications.orders, " +
                "notifications.statusfeed, notifications.resourcelimitlog, " +
                "notifications.deaddeliveryreports CASCADE;");
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Factory] Database cleanup failed (non-fatal): {ex.Message}");
        }
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
                throw ex.InnerException;
            }
        }
    }

    private static string FindMigrationPath()
    {
        string? currentDir = AppContext.BaseDirectory;

        for (int i = 0; i < 10; i++)
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
            if (currentDir == null)
            {
                break;
            }

            string migrationPath = Path.Combine(currentDir, "src", "Altinn.Notifications.Persistence", "Migration");
            if (Directory.Exists(migrationPath))
            {
                return migrationPath;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not find Migration directory. Expected structure: <repo>/components/api/src/Altinn.Notifications.Persistence/Migration. " +
            $"Searched up to 10 parent directories from: {AppContext.BaseDirectory}");
    }
}
