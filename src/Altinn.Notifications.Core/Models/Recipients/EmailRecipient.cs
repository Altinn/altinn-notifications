namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Class representing a email recipient
/// </summary>
public class EmailRecipient
{
    /// <summary>
    /// Gets or sets the recipient id
    /// </summary>
    public string? RecipientId { get; set; } = null;

    /// <summary>
    /// Gets or sets the toaddress
    /// </summary>
    public string ToAddress { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailRecipient"/> class.
    /// </summary>
    public EmailRecipient(string recipientId, string toAddress)
    {
        RecipientId = recipientId;
        ToAddress = toAddress;
    }
}