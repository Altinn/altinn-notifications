namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents a request for sending an email to a specific recipient.
/// </summary>
public class RecipientEmailRequest
{
    /// <summary>
    /// Gets or sets the email address of the intended recipient.
    /// </summary>
    /// <remarks>
    /// This is the destination address where the email will be delivered.
    /// </remarks>
    public required string EmailAddress { get; set; }

    /// <summary>
    /// Gets or sets the configuration options for the email message.
    /// </summary>
    /// <remarks>
    /// These settings control how and when the email will be composed and delivered to the recipient.
    /// </remarks>
    public required EmailSendingOptions Settings { get; set; }
}
