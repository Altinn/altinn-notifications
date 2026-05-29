using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Publishers;
using Microsoft.Extensions.DependencyInjection;

using Wolverine;

namespace Altinn.Notifications.Email.Integrations.Publishers;

/// <summary>
/// Azure Service Bus–based implementation of <see cref="IEmailServiceRateLimitDispatcher"/> that dispatches
/// an <see cref="EmailServiceRateLimitCommand"/> via Wolverine to publish rate-limit events.
/// </summary>
public class EmailServiceRateLimitPublisher : WolverinePublisher, IEmailServiceRateLimitDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailServiceRateLimitPublisher"/> class.
    /// </summary>
    /// <param name="serviceProvider">
    /// The service provider used to resolve a scoped <see cref="IMessageBus"/> instance for each dispatch.
    /// </param>
    public EmailServiceRateLimitPublisher(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(GenericServiceUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        var command = new EmailServiceRateLimitCommand
        {
            Data = update.Data,
            Source = update.Source
        };

        await PublishCommandAsync(command);
    }
}
