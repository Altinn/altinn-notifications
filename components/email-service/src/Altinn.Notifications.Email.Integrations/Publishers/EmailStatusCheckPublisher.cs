using Altinn.Notifications.Email.Core;
using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Models;
using Altinn.Notifications.Shared.Publishers;

using Wolverine;

namespace Altinn.Notifications.Email.Integrations.Publishers;

/// <summary>
/// Azure Service Bus–based implementation of <see cref="IEmailStatusCheckDispatcher"/> that dispatches a
/// <see cref="CheckEmailSendStatusCommand"/> via Wolverine to initiate status tracking for an email send
/// operation processed by Azure Communication Services (ACS).
/// </summary>
public class EmailStatusCheckPublisher : WolverinePublisher, IEmailStatusCheckDispatcher
{
    private const int _statusPollDelayMs = 8000;
    private readonly IDateTimeService _dateTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailStatusCheckPublisher"/> class.
    /// </summary>
    /// <param name="serviceProvider">
    /// The Wolverine message bus responsible for sending <see cref="CheckEmailSendStatusCommand"/> messages to Azure Service Bus.
    /// </param>
    /// <param name="dateTime">
    /// Provides the current UTC timestamp applied to the command as the initial status‑check time.
    /// </param>
    public EmailStatusCheckPublisher(IServiceProvider serviceProvider, IDateTimeService dateTime) : base(serviceProvider)
    {
        _dateTime = dateTime;
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(Guid notificationId, string operationId, long? encodedAttachmentsSize = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentOutOfRangeException.ThrowIfEqual(notificationId, Guid.Empty);

        var checkEmailSendStatusCommand = new CheckEmailSendStatusCommand
        {
            SendOperationId = operationId,
            NotificationId = notificationId,
            LastCheckedAtUtc = _dateTime.UtcNow(),
            EncodedAttachmentsSize = encodedAttachmentsSize
        };

        var deliveryOptions = new DeliveryOptions
        {
            ScheduleDelay = TimeSpan.FromMilliseconds(_statusPollDelayMs)
        };

        await PublishCommandAsync(checkEmailSendStatusCommand, deliveryOptions);
    }
}
