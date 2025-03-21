namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents a request to create a notification order with non or more reminders.
/// Inherits the scheduling options from <see cref="NotificationOrderScheduling"/>.
/// </summary>
public class NotificationOrderSequenceRequest : NotificationOrderScheduling
{
    /// <summary>
    /// Gets or sets optional identifiers for one or more dialogs or transmissions in Dialogporten.
    /// </summary>
    /// <remarks>
    /// When specified, this associates the notification with specific dialogs or transmissions
    /// in the Dialogporten service, enabling integration between notifications and Dialogporten.
    /// </remarks>
    public DialogportenReference? DialogportenAssociation { get; set; }

    /// <summary>
    /// Gets or sets the idempotency identifier defined by the sender.
    /// </summary>
    public required string IdempotencyId { get; set; }
}
