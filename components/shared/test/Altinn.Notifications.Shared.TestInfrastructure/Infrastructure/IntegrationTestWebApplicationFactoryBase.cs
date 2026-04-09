using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wolverine;

namespace Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

/// <summary>
/// Abstract base class for ASB integration test web application factories.
/// Handles all shared machinery: configuration loading, service overrides, host lifecycle, and disposal.
/// </summary>
/// <typeparam name="TProgram">The application entry point type.</typeparam>
/// <typeparam name="TSelf">The concrete factory type (CRTP) enabling fluent method chaining.</typeparam>
public abstract class IntegrationTestWebApplicationFactoryBase<TProgram, TSelf>(IntegrationTestContainersFixture fixture)
    : WebApplicationFactory<TProgram>
    where TProgram : class
    where TSelf : IntegrationTestWebApplicationFactoryBase<TProgram, TSelf>
{
    private IHost _host = null!;
    private readonly List<Action<IServiceCollection>> _configureTestServices = [];
    private readonly Dictionary<string, string?> _configOverrides = [];

    /// <summary>
    /// Gets the fixture providing container connection strings.
    /// </summary>
    protected IntegrationTestContainersFixture Fixture { get; } = fixture;

    /// <summary>
    /// Gets the IHost instance. Access this after calling Initialize().
    /// </summary>
    public IHost Host => _host ?? throw new InvalidOperationException("Host not created yet. Call Initialize() first.");

    /// <summary>
    /// Initializes the factory by creating the test client, triggering host startup.
    /// </summary>
    public TSelf Initialize()
    {
        _ = CreateClient();
        return (TSelf)this;
    }

    /// <summary>
    /// Adds an in-memory configuration override applied after appsettings.integrationtest.json.
    /// Use this to enable or disable specific settings per test. Must be called before Initialize().
    /// </summary>
    public TSelf WithConfig(string key, string? value)
    {
        _configOverrides[key] = value;
        return (TSelf)this;
    }

    /// <summary>
    /// Configures additional test services. Use this to replace services with mocks.
    /// Must be called before Initialize().
    /// </summary>
    public TSelf ConfigureTestServices(Action<IServiceCollection> configure)
    {
        _configureTestServices.Add(configure);
        return (TSelf)this;
    }

    /// <summary>
    /// Sends a message to a specific ASB queue using Wolverine's endpoint routing.
    /// </summary>
    public async Task SendToQueueAsync<T>(string queueName, T message)
        where T : class
    {
        using var scope = Host.Services.CreateScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var endpoint = messageBus.EndpointFor(new Uri($"asb://queue/{queueName}"));
        await endpoint.SendAsync(message);
    }

    /// <summary>
    /// Sends a message to a specific named endpoint using Wolverine's transport.
    /// Use this when the component only has a listener (no publisher route) for the message type,
    /// for example when simulating a message arriving from another component's queue.
    /// </summary>
    public async Task SendToEndpointAsync<T>(string endpointName, T message)
        where T : class
    {
        using var scope = Host.Services.CreateScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var endpoint = messageBus.EndpointFor(endpointName);
        await endpoint.SendAsync(message);
    }

    /// <summary>
    /// Replaces a service registration with a test implementation, preserving the original lifetime.
    /// </summary>
    public TSelf ReplaceService<TService>(Func<IServiceProvider, TService> implementationFactory)
        where TService : class
    {
        ConfigureTestServices(services =>
        {
            var descriptors = services.Where(d => d.ServiceType == typeof(TService)).ToList();
            var lifetime = descriptors.LastOrDefault()?.Lifetime ?? ServiceLifetime.Singleton;

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            services.Add(new ServiceDescriptor(typeof(TService), sp => implementationFactory(sp), lifetime));
        });

        return (TSelf)this;
    }

    /// <summary>
    /// Returns base configuration overrides derived from the container fixture (e.g. connection strings).
    /// These are applied before per-test <see cref="WithConfig"/> overrides.
    /// </summary>
    protected abstract Dictionary<string, string?> GetFixtureConfigOverrides();

    /// <summary>
    /// Configures component-specific services: removes Kafka consumers, replaces producers, etc.
    /// </summary>
    protected abstract void ConfigureComponentServices(IConfiguration configuration, IServiceCollection services);

    /// <summary>
    /// Drains Service Bus dead-letter queues after each test. Override to drain component-specific queues.
    /// </summary>
    protected virtual Task DrainQueuesAsync() => Task.CompletedTask;

    /// <summary>
    /// Performs component-specific cleanup (e.g. deleting database rows) after each test.
    /// </summary>
    protected virtual Task CleanupAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.SetBasePath(AppContext.BaseDirectory);
            config.AddJsonFile("appsettings.integrationtest.json", optional: false, reloadOnChange: false);

            var overrides = GetFixtureConfigOverrides();
            foreach (var (key, value) in _configOverrides)
            {
                overrides[key] = value;
            }

            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices((context, services) =>
        {
            ConfigureComponentServices(context.Configuration, services);

            foreach (var configure in _configureTestServices)
            {
                configure(services);
            }
        });

        builder.UseEnvironment("Development");

        _host = base.CreateHost(builder);
        return _host;
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        // Give time for any in-flight messages to be processed and queues to stabilize before shutdown.
        await Task.Delay(500);

        try
        {
            await base.DisposeAsync();
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is ObjectDisposedException { ObjectName: "EventLogInternal" }))
        {
            // Suppress the EventLogInternal disposed error that occurs during Wolverine shutdown
            // when the Windows Event Log logger is disposed before Wolverine finishes draining.
        }
        finally
        {
            await CleanupAsync();
            await DrainQueuesAsync();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Truncates a string to the specified maximum length for console output.
    /// </summary>
    protected static string Truncate(string? value, int maxLength) =>
        value is null ? "(null)" : value[..Math.Min(value.Length, maxLength)];
}
