using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.DependencyInjection;

using Wolverine;

namespace Altinn.Notifications.Email.Integrations.Publishers;

/// <summary>
/// Azure Service Bus–based implementation of <see cref="IEmailServiceRateLimitDispatcher"/> that dispatches
/// an <see cref="EmailServiceRateLimitCommand"/> via Wolverine to publish rate-limit events.
/// This implementation is active when <c>WolverineSettings:EnableEmailServiceRateLimitPublisher</c> is set to <c>true</c>.
/// </summary>
public class EmailServiceRateLimitPublisher : IEmailServiceRateLimitDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailServiceRateLimitPublisher"/> class.
    /// </summary>
    /// <param name="serviceProvider">
    /// The service provider used to resolve a scoped <see cref="IMessageBus"/> instance for each dispatch.
    /// </param>
    public EmailServiceRateLimitPublisher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(GenericServiceUpdate update)
    {
        var command = new EmailServiceRateLimitCommand
        {
            Data = update.Data,
            Source = update.Source
        };

        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await messageBus.SendAsync(command);
    }
}
