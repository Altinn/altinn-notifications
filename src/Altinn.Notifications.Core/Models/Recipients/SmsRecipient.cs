namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Class representing an sms recipient
/// </summary>
public class SmsRecipient
{
    /// <summary>
    /// Gets or sets the recipient id
    /// </summary>
    public string? RecipientId { get; set; } = null;

    /// <summary>
    /// Gets or sets the mobile number
    /// </summary>
    public string MobileNumber { get; set; } = string.Empty;
}
