namespace Altinn.Notifications.Shared.Commands;

/// <summary>
/// Represents a command to send an SMS notification from the Notifications API to the SMS Service.
/// </summary>
public sealed record SendSmsCommand
{
    /// <summary>
    /// Gets the mobile phone number associated with the user.
    /// </summary>
    /// <remarks>The mobile number is represented as a string and is intended for use in contact or
    /// notification scenarios. If not explicitly set, the property is initialized to an empty string.</remarks>
    public string MobileNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets the content of the message body.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets the phone number of the sender.
    /// </summary>
    /// <remarks>The sender's phone number can be used for identification or contact purposes in SMS
    /// communications. This property is initialized to an empty string if not set.</remarks>
    public string SenderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets the unique identifier for the notification.
    /// </summary>
    public Guid NotificationId { get; set; }
}
