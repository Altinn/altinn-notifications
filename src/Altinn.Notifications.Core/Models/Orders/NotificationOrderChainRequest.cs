using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents the core business entity of a notification order request with reminders.
/// </summary>
public class NotificationOrderChainRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationOrderChainRequest"/> class.
    /// </summary>
    /// <param name="orderId">The unique identifier for the main notification order in this sequence.</param>
    /// <param name="creator">The creator of the notification request.</param>
    /// <param name="idempotencyId">The idempotency identifier defined by the sender.</param>
    /// <param name="recipient">The recipient information for this notification.</param>
    /// <param name="conditionEndpoint">A URI endpoint that can determine whether the notification should be sent.</param>
    /// <param name="dialogportenAssociation">Optional identifiers for one or more dialogs or transmissions in Dialogporten.</param>
    /// <param name="reminders">A list of reminders that may be triggered after the initial notification has been processed.</param>
    /// <param name="requestedSendTime">The earliest date and time when the notification should be delivered.</param>
    /// <param name="sendersReference">The sender's reference identifier.</param>
    public NotificationOrderChainRequest(
        Guid orderId,
        Creator creator,
        string idempotencyId,
        NotificationRecipient recipient,
        Uri? conditionEndpoint = null,
        DialogportenIdentifiers? dialogportenAssociation = null,
        List<NotificationReminder>? reminders = null,
        DateTime? requestedSendTime = null,
        string? sendersReference = null)
    {
        OrderId = orderId;
        Creator = creator;
        IdempotencyId = idempotencyId;
        Recipient = recipient;
        ConditionEndpoint = conditionEndpoint;
        DialogportenAssociation = dialogportenAssociation;
        Reminders = reminders;
        RequestedSendTime = requestedSendTime ?? DateTime.UtcNow;
        SendersReference = sendersReference;
    }

    /// <summary>
    /// Gets a URI endpoint that can determine whether the notification should be sent.
    /// </summary>
    /// <remarks>
    /// When specified, the system will call this endpoint before sending the notification.
    /// The notification will only be sent if the endpoint returns a positive response.
    /// This enables conditional delivery based on external business rules or state.
    /// </remarks>
    public Uri? ConditionEndpoint { get; internal set; }

    /// <summary>
    /// Gets the creator of the notification order sequence request.
    /// </summary>
    public Creator Creator { get; internal set; }

    /// <summary>
    /// Gets the optional identifiers for one or more dialogs or transmissions in Dialogporten.
    /// </summary>
    /// <remarks>
    /// When specified, this associates the notification with specific dialogs or transmissions
    /// in the Dialogporten service, enabling integration between notifications and Dialogporten.
    /// </remarks>
    public DialogportenIdentifiers? DialogportenAssociation { get; internal set; }

    /// <summary>
    /// Gets the idempotency identifier defined by the sender.
    /// </summary>
    public string IdempotencyId { get; internal set; }

    /// <summary>
    /// Gets the unique identifier for the main notification order in the sequence.
    /// </summary>
    /// <value>
    /// A <see cref="Guid"/> representing the unique identifier of the main notification order.
    /// </value>
    public Guid OrderId { get; internal set; }

    /// <summary>
    /// Gets the recipient information for this notification.
    /// </summary>
    /// <remarks>
    /// Specifies the target recipient through one of the supported channels:
    /// email address, SMS number, national identity number, or organization number.
    /// The reminder can be directed to a different recipient than the initial notification.
    /// </remarks>
    public NotificationRecipient Recipient { get; internal set; }

    /// <summary>
    /// Gets a list of reminders that may be triggered under certain conditions after the initial notification has been processed.
    /// </summary>
    /// <remarks>
    /// Each reminder can have its own recipient settings, delay period, and triggering conditions.
    /// </remarks>
    public List<NotificationReminder>? Reminders { get; internal set; }

    /// <summary>
    /// Gets the earliest date and time when the notification should be delivered.
    /// </summary>
    /// <remarks>
    /// Allows scheduling notifications for future delivery. The system will not deliver the notification
    /// before this time, but may deliver it later depending on system load and availability.
    /// Defaults to the current UTC time if not specified.
    /// </remarks>
    public DateTime RequestedSendTime { get; internal set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the sender's reference identifier.
    /// </summary>
    /// <remarks>
    /// An optional identifier used to correlate the notification with records in the sender's system.
    /// </remarks>
    public string? SendersReference { get; internal set; }
}
