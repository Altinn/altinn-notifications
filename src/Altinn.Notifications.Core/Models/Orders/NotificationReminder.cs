using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents a reminder that can be sent following the associated initial notification order.
/// </summary>
public class NotificationReminder
{
    /// <summary>
    /// Gets or sets the condition endpoint used to determine if the reminder should be sent.
    /// </summary>
    /// <remarks>
    /// When specified, the system will call this endpoint before sending the reminder.
    /// The reminder will only be sent if the endpoint returns a positive response.
    /// This allows for dynamic decision-making about whether the reminder is still relevant.
    /// </remarks>
    public Uri? ConditionEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the number of days to delay this reminder.
    /// </summary>
    /// <value>
    /// The number of days to delay the reminder.
    /// </value>
    public required int DelayDays { get; set; } = 1;

    /// <summary>
    /// Gets the unique identifier for the associated notification order.
    /// </summary>
    /// <value>
    /// A <see cref="Guid"/> representing the unique identifier of the associated notification order.
    /// </value>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Gets or sets the recipient information for this reminder.
    /// </summary>
    /// <remarks>
    /// Specifies the target recipient through one of the supported channels:
    /// email address, SMS number, national identity number, or organization number.
    /// The reminder can be directed to a different recipient than the initial notification.
    /// </remarks>
    public required NotificationRecipient Recipient { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the associated Email or SMS can be sent at the earliest.
    /// </summary>
    /// <value>
    /// The requested send time, which can be null and defaults to the current date and time.
    /// </value>
    public DateTime RequestedSendTime { get; set; }

    /// <summary>
    /// Gets or sets the sender's reference for this reminder.
    /// </summary>
    /// <remarks>
    /// A unique identifier used by the sender to correlate this reminder with their internal systems.
    /// </remarks>
    public string? SendersReference { get; set; }

    /// <summary>
    /// Gets the type of the reminder.
    /// </summary>
    /// <remarks>
    /// Specifies that this is a reminder.
    /// </remarks>
    public OrderType Type { get; set; } = OrderType.Reminder;
}
