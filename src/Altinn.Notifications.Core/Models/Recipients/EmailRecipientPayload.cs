namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents a request for sending an email to a specific recipient.
/// </summary>
public class EmailRecipientPayload
{
    /// <summary>
    /// Gets or sets the email address of the intended recipient.
    /// </summary>
    public required string EmailAddress { get; set; }

    /// <summary>
    /// Gets or sets the configuration for the email, including the subject, body, and sending time policy.
    /// </summary>
    public required EmailRecipientPayloadSettings Settings { get; set; }
}
