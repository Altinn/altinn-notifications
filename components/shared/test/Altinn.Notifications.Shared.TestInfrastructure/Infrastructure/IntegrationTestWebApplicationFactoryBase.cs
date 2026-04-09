using Azure.Messaging.ServiceBus;

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
        catch (AggregateException ex) when (ex.Flatten().InnerExceptions.All(e =>
            e is ObjectDisposedException { ObjectName: "EventLogInternal" } ||
            e is TimeoutException))
        {
            // Suppress errors that occur during Wolverine shutdown on the inline ASB listener:
            //
            // 1. TimeoutException — the AMQP link drain exceeds its 3-second budget; expected
            //    for inline listeners that hold an open receiver until the process exits.
            //
            // 2. ObjectDisposedException(EventLogInternal) — Wolverine tries to log the timeout
            //    error but the Windows EventLog provider has already been torn down.
            //
            // Wolverine may nest these inside multiple layers of AggregateException, so we call
            // Flatten() before checking so every leaf exception is covered.
        }
        finally
        {
            await CleanupAsync();
            await DrainQueuesAsync();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Removes all registered services whose implementation type is assignable to <paramref name="baseType"/>.
    /// Use this to strip component-specific consumers (e.g. Kafka) before tests run.
    /// </summary>
    protected static void RemoveServicesAssignableTo(IServiceCollection services, Type baseType)
    {
        var toRemove = services
            .Where(s => s.ImplementationType?.IsAssignableTo(baseType) == true)
            .ToList();

        foreach (var descriptor in toRemove)
        {
            services.Remove(descriptor);
        }
    }

    /// <summary>
    /// Drains all messages from the dead-letter sub-queues for each of the given <paramref name="queueNames"/>.
    /// Silently skips blank names. Exceptions are logged but not rethrown.
    /// </summary>
    protected static async Task DrainDeadLetterQueuesAsync(string connectionString, params string[] queueNames)
    {
        queueNames = Array.FindAll(queueNames, n => !string.IsNullOrWhiteSpace(n));

        try
        {
            await using var client = new ServiceBusClient(connectionString);

            foreach (var queueName in queueNames)
            {
                await using var receiver = client.CreateReceiver($"{queueName}/$deadletterqueue");

                while (true)
                {
                    var message = await receiver.ReceiveMessageAsync(TimeSpan.FromMilliseconds(500));
                    if (message == null)
                    {
                        break;
                    }

                    await receiver.CompleteMessageAsync(message);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Factory] DLQ drain failed (non-fatal): {ex.Message}");
        }
    }

    /// <summary>
    /// Truncates a string to the specified maximum length for console output.
    /// </summary>
    protected static string Truncate(string? value, int maxLength) =>
        value is null ? "(null)" : value[..Math.Min(value.Length, maxLength)];
}
