namespace Altinn.Notifications.Core.Enums;

/// <summary>
/// Identifies the channel a sender address belongs to. The values correspond to the ids in the notifications.senderaddresstype table.
/// </summary>
public enum SenderAddressType
{
    /// <summary>
    /// A sender for SMS notifications, either a mobile number or an alphanumeric sender name.
    /// </summary>
    Sms = 1,

    /// <summary>
    /// A sender address for email notifications.
    /// </summary>
    Email = 2
}
