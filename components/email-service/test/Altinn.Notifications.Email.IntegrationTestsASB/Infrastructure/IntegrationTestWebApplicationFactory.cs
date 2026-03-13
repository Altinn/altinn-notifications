using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Integrations.Kafka.Consumers;
using Altinn.Notifications.Shared.TestInfrastructure.Infrastructure;

using Azure.Messaging.ServiceBus;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Moq;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure;

/// <summary>
/// WebApplicationFactory for email service ASB integration tests.
/// Boots the real Program.cs with test-specific overrides.
/// </summary>
public class IntegrationTestWebApplicationFactory(IntegrationTestContainersFixture fixture) : WebApplicationFactory<Program>
{
    private readonly IntegrationTestContainersFixture _fixture = fixture;
    private IHost _host = null!;
    private readonly List<Action<IServiceCollection>> _configureTestServices = [];

    /// <summary>
    /// Gets the IHost instance for use with Wolverine's IMessageBus.
    /// Access this after calling CreateClient() or Initialize().
    /// </summary>
    public IHost Host => _host ?? throw new InvalidOperationException("Host not created yet. Call CreateClient() or Initialize() first.");

    /// <summary>
    /// Configures additional test services. Use this to replace services with mocks.
    /// Must be called before CreateClient().
    /// </summary>
    public IntegrationTestWebApplicationFactory ConfigureTestServices(Action<IServiceCollection> configure)
    {
        _configureTestServices.Add(configure);
        return this;
    }

    /// <inheritdoc/>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.SetBasePath(AppContext.BaseDirectory);
            config.AddJsonFile("appsettings.integrationtest.json", optional: false, reloadOnChange: false);

            var testConfigOverrides = new Dictionary<string, string?>
            {
                ["WolverineSettings:ServiceBusConnectionString"] = _fixture.ServiceBusConnectionString,
            };
            config.AddInMemoryCollection(testConfigOverrides);
        });

        builder.ConfigureServices((context, services) =>
        {
            Console.WriteLine($"[EmailFactory] ServiceBus connection: {Truncate(_fixture.ServiceBusConnectionString, 50)}...");

            // Remove all Kafka services - they are not needed in ASB tests
            // and the CommonProducer constructor crashes without a running broker
            var consumersToRemove = services
                .Where(s => s.ImplementationType?.IsAssignableTo(typeof(KafkaConsumerBase)) == true)
                .ToList();

            foreach (var descriptor in consumersToRemove)
            {
                services.Remove(descriptor);
            }

            services.Replace(ServiceDescriptor.Singleton(Mock.Of<ICommonProducer>()));

            // Apply any additional test service configuration
            foreach (var configure in _configureTestServices)
            {
                configure(services);
            }
        });

        builder.UseEnvironment("Development");

        _host = base.CreateHost(builder);
        return _host;
    }

    private static string Truncate(string? value, int maxLength) =>
        value is null ? "(null)" : value[..Math.Min(value.Length, maxLength)];

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        await Task.Delay(150);

        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            await DrainDeadLetterQueueAsync();
            GC.SuppressFinalize(this);
        }
    }

    private async Task DrainDeadLetterQueueAsync()
    {
        try
        {
            await using var client = new ServiceBusClient(_fixture.ServiceBusConnectionString);
            await using var receiver = client.CreateReceiver("smoke-test/$deadletterqueue");

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
        catch (Exception ex)
        {
            Console.WriteLine($"[EmailFactory] DLQ drain failed (non-fatal): {ex.Message}");
        }
    }
}
