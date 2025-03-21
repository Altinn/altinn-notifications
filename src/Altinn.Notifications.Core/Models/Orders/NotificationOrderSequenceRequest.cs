using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents a request to create a notification order with one or more reminders.
/// Inherits the common data fragment from <see cref="NotificationOrderRequestScheduling"/>.
/// </summary>
public class NotificationOrderSequenceRequest : NotificationOrderRequestScheduling
{
    /// <summary>
    /// Gets or sets optional identifiers for one or more dialogs or transmissions in Dialogporten.
    /// </summary>
    public DialogportenAssociation? DialogportenAssociation { get; set; }

    /// <summary>
    /// Gets or sets the idempotency identifier defined by the sender.
    /// </summary>
    public required string IdempotencyId { get; set; }

    /// <summary>
    /// Gets the order identifier.
    /// </summary>
    /// <value>
    /// The order identifier.
    /// </value>
    public Guid OrderId { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets the creator.
    /// </summary>
    /// <value>
    /// The creator.
    /// </value>
    public Creator Creator { get; set; }

    /// <summary>
    /// Gets the created.
    /// </summary>
    /// <value>
    /// The created.
    /// </value>
    public DateTime Created { get; internal set; }

    /// <summary>
    /// Gets or sets the required recipient information, whether for mobile number, email-address, national identity, or organization number.
    /// </summary>
    public required AssociatedRecipients Recipient { get; set; }

    /// <summary>
    /// Gets or sets a list of reminders that may be triggered under certain conditions after the initial notification has been processed.
    /// </summary>
    public List<NotificationReminder>? Reminders { get; set; }

    /// <summary>
    /// Gets or sets the notification order.
    /// </summary>
    /// <value>
    /// The notification order.
    /// </value>
    public IEnumerable<NotificationOrder>? NotificationOrders { get; set; }
}
