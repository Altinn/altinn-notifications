using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;

using Microsoft.Extensions.DependencyInjection;

using Wolverine;

namespace Altinn.Notifications.Email.Integrations.Producers;

/// <summary>
/// Azure Service Bus–based implementation of <see cref="IEmailStatusCheckDispatcher"/> that dispatches a
/// <see cref="CheckEmailSendStatusCommand"/> via Wolverine to initiate status tracking for an email send
/// operation processed by Azure Communication Services (ACS).
/// This implementation is active when <c>WolverineSettings:EnableCheckEmailSendStatusListener</c> is set to <c>true</c>.
/// </summary>
public class EmailStatusCheckPublisher : IEmailStatusCheckDispatcher
{
    private readonly IDateTimeService _dateTime;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailStatusCheckPublisher"/> class.
    /// </summary>
    /// <param name="serviceProvider">
    /// The Wolverine message bus responsible for sending <see cref="CheckEmailSendStatusCommand"/> messages to Azure Service Bus.
    /// </param>
    /// <param name="dateTime">
    /// Provides the current UTC timestamp applied to the command as the initial status‑check time.
    /// </param>
    public EmailStatusCheckPublisher(IServiceProvider serviceProvider, IDateTimeService dateTime)
    {
        _dateTime = dateTime;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public Task DispatchAsync(Guid notificationId, string operationId)
    {
        var checkEmailSendStatusCommand = new CheckEmailSendStatusCommand
        {
            SendOperationId = operationId,
            NotificationId = notificationId,
            LastCheckedAtUtc = _dateTime.UtcNow()
        };

        // Task.Run escapes the Wolverine handler's ambient AsyncLocal message context,
        // ensuring PublishAsync uses the configured PublishMessage<T> routing rules
        // rather than inheriting the parent handler's ReplyTo queue.
        return Task.Run(async () =>
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await messageBus.PublishAsync(checkEmailSendStatusCommand);
        });
    }
}
