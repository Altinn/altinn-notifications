using Altinn.Notifications.Email.IntegrationTestsASB.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Wolverine;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Extensions;

/// <summary>
/// Utility extension methods for common test patterns with IntegrationTestWebApplicationFactory.
/// </summary>
public static class TestFactoryExtensions
{
    /// <summary>
    /// Initializes the factory and returns it ready for use.
    /// Creates the client which triggers host initialization.
    /// </summary>
    public static IntegrationTestWebApplicationFactory Initialize(this IntegrationTestWebApplicationFactory factory)
    {
        _ = factory.CreateClient();
        return factory;
    }

    /// <summary>
    /// Publishes a message using Wolverine's message bus.
    /// </summary>
    public static async Task PublishMessageAsync<T>(this IntegrationTestWebApplicationFactory factory, T message)
        where T : class
    {
        using var scope = factory.Host.Services.CreateScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await messageBus.PublishAsync(message);
    }

    /// <summary>
    /// Replaces a service registration with a mock or test implementation.
    /// </summary>
    public static IntegrationTestWebApplicationFactory ReplaceService<TService>(
        this IntegrationTestWebApplicationFactory factory,
        Func<IServiceProvider, TService> implementationFactory)
        where TService : class
    {
        factory.ConfigureTestServices(services =>
        {
            var descriptors = services
                .Where(d => d.ServiceType == typeof(TService))
                .ToList();

            var lifetime = descriptors.LastOrDefault()?.Lifetime ?? ServiceLifetime.Singleton;

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            services.Add(new ServiceDescriptor(
                typeof(TService),
                serviceProvider => implementationFactory(serviceProvider),
                lifetime));
        });

        return factory;
    }
}
