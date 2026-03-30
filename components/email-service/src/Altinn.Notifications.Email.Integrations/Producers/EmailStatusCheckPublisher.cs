using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;

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
    private readonly IMessageBus _messageBus;
    private readonly IDateTimeService _dateTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailStatusCheckPublisher"/> class.
    /// </summary>
    /// <param name="messageBus">
    /// The Wolverine message bus responsible for sending <see cref="CheckEmailSendStatusCommand"/> messages to Azure Service Bus.
    /// </param>
    /// <param name="dateTime">
    /// Provides the current UTC timestamp applied to the command as the initial status‑check time.
    /// </param>
    public EmailStatusCheckPublisher(IMessageBus messageBus, IDateTimeService dateTime)
    {
        _dateTime = dateTime;
        _messageBus = messageBus;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(Guid notificationId, string operationId)
    {
        var command = new CheckEmailSendStatusCommand
        {
            SendOperationId = operationId,
            NotificationId = notificationId,
            LastCheckedAtUtc = _dateTime.UtcNow()
        };

        await _messageBus.SendAsync(command);
    }
}
