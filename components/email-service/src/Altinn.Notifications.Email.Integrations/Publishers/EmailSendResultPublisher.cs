using Altinn.Notifications.Email.Core.Dependencies;
using Altinn.Notifications.Email.Core.Status;
using Altinn.Notifications.Shared.Commands;
using Altinn.Notifications.Shared.Publishers;

using Wolverine;

namespace Altinn.Notifications.Email.Integrations.Publishers;

/// <summary>
/// Azure Service Bus–based implementation of <see cref="IEmailSendResultDispatcher"/> that dispatches
/// an <see cref="EmailSendResultCommand"/> via Wolverine to publish terminal email send operation results.
/// </summary>
public class EmailSendResultPublisher : WolverinePublisher, IEmailSendResultDispatcher
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSendResultPublisher"/> class.
    /// </summary>
    /// <param name="serviceProvider">
    /// The service provider used to resolve a scoped <see cref="IMessageBus"/> instance for each dispatch.
    /// </param>
    public EmailSendResultPublisher(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(SendOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.SendResult is null)
        {
            throw new ArgumentException("SendResult must be set before dispatching.", nameof(result));
        }

        if (result.NotificationId is null)
        {
            throw new ArgumentException("NotificationId must be set before dispatching.", nameof(result));
        }

        if (result.NotificationId == Guid.Empty)
        {
            throw new ArgumentException("NotificationId must not be empty.", nameof(result));
        }

        var command = new EmailSendResultCommand
        {
            NotificationId = result.NotificationId.Value,
            SendResult = result.SendResult.Value.ToString(),
            EncodedAttachmentsSize = result.EncodedAttachmentsSize,
            OperationId = string.IsNullOrWhiteSpace(result.OperationId) ? null : result.OperationId
        };

        await PublishCommandAsync(command);
    }
}
