using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Shared.Commands;

using Microsoft.Extensions.DependencyInjection;

using Wolverine;

namespace Altinn.Notifications.Email.Integrations.Publishers;

/// <summary>
/// Azure Service Bus–based implementation of <see cref="IEmailSendResultDispatcher"/> that dispatches
/// an <see cref="EmailSendResultCommand"/> via Wolverine to publish terminal email send operation results.
/// This implementation is active when <c>WolverineSettings:EnableEmailSendResultPublisher</c> is set to <c>true</c>.
/// </summary>
public class EmailSendResultPublisher : IEmailSendResultDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSendResultPublisher"/> class.
    /// </summary>
    /// <param name="serviceProvider">
    /// The service provider used to resolve a scoped <see cref="IMessageBus"/> instance for each dispatch.
    /// </param>
    public EmailSendResultPublisher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(SendOperationResult result)
    {
        var command = new EmailSendResultCommand
        {
            OperationId = result.OperationId,
            NotificationId = result.NotificationId ?? Guid.Empty,
            SendResult = result.SendResult?.ToString() ?? string.Empty
        };

        await using var scope = _serviceProvider.CreateAsyncScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await messageBus.SendAsync(command);
    }
}
