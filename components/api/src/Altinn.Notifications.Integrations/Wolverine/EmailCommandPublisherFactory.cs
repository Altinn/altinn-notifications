using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;

using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Integrations.Wolverine;

/// <summary>
/// Factory implementation for creating <see cref="IEmailCommandPublisher"/> instances.
/// This factory resolves the service lifetime mismatch between singleton services
/// and scoped Wolverine dependencies by creating new service scopes when needed.
/// </summary>
public class EmailCommandPublisherFactory : IEmailCommandPublisherFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailCommandPublisherFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider to create scoped instances.</param>
    public EmailCommandPublisherFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public IEmailCommandPublisher CreatePublisher()
    {
        // Create a scope and resolve the scoped IEmailCommandPublisher
        using var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IEmailCommandPublisher>();
    }

    /// <inheritdoc/>
    public async Task<Guid?> PublishAsync(Email email, CancellationToken cancellationToken)
    {
        // Create a scope for the scoped dependencies (IMessageBus via Wolverine)
        using var scope = _serviceProvider.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IEmailCommandPublisher>();
        
        return await publisher.PublishAsync(email, cancellationToken);
    }
}
