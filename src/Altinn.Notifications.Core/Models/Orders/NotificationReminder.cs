using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents a reminder that can be sent following the associated initial notification order.
/// </summary>
public class NotificationReminder
{
    /// <summary>
    /// Gets or sets the condition endpoint used to check the sending condition.
    /// </summary>
    /// <value>
    /// A URI that determines if the associated notifications should be sent based on certain conditions.
    /// </value>
    public Uri? ConditionEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the optional number of days to delay.
    /// The reminder will be processed on or after (RequestedSendTime + DelayDays).
    /// </summary>
    public int? DelayDays { get; set; }

    /// <summary>
    /// Gets or sets the required recipient information, whether for mobile number, email-address, national identity, or organization number.
    /// </summary>
    public required AssociatedRecipients Recipient { get; set; } = new();

    /// <summary>
    /// Gets or sets the sender's reference.
    /// </summary>
    /// <value>
    /// A reference used to identify the notification order in the sender's system.
    /// </value>
    public string? SendersReference { get; set; }
}
