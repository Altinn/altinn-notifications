using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents configuration settings that are associated with the request model for sending an E-mail to a specific recipient.
/// </summary>
public class EmailRecipientPayloadSettings
{
    /// <summary>
    /// Gets or sets the main body content of the email.
    /// </summary>
    public required string Body { get; set; }

    /// <summary>
    /// Gets or sets the customized body of the email after replacing the keywords with actual values.
    /// </summary>
    public string? CustomizedBody { get; set; } = null;

    /// <summary>
    /// Gets or sets the customized subject of the email after replacing the keywords with actual values.
    /// </summary>
    public string? CustomizedSubject { get; set; } = null;

    /// <summary>
    /// Gets or sets the content type (plain text or HTML) of the email.
    /// Defaults to <see cref="EmailContentType.Plain"/>.
    /// </summary>
    public EmailContentType ContentType { get; set; } = EmailContentType.Plain;

    /// <summary>
    /// Gets or sets the sender's email address. This value determines
    /// which address will appear as the sender in the recipient's mailbox.
    /// </summary>
    public string? SenderEmailAddress { get; set; }

    /// <summary>
    /// Gets or sets the display name of the sender.
    /// Can only be used if <see cref="SenderEmailAddress"/> is set.
    /// </summary>
    public string? SenderName { get; set; }

    /// <summary>
    /// Gets or sets the policy defining when the email should be sent.
    /// Defaults to <see cref="SendingTimePolicy.WorkingDaysDaytime"/>.
    /// </summary>
    public SendingTimePolicy SendingTimePolicy { get; set; } = SendingTimePolicy.WorkingDaysDaytime;

    /// <summary>
    /// Gets or sets the subject line of the email.
    /// </summary>
    public required string Subject { get; set; }
}
