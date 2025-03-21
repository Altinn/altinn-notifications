using System.ComponentModel.DataAnnotations;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Defines email configuration settings used in notification orders.
/// </summary>
public class EmailSendingOptions
{
    /// <summary>
    /// Gets or sets the display name of the sender.
    /// </summary>
    /// <remarks>
    /// Can only be used if <see cref="SenderEmailAddress"/> is set.
    /// Appears alongside the email address in the recipient's email client.
    /// </remarks>
    public string? SenderName { get; set; }

    /// <summary>
    /// Gets or sets the sender's email address.
    /// </summary>
    /// <remarks>
    /// This value determines which address will appear as the sender in the recipient's mailbox.
    /// </remarks>
    public string? SenderEmailAddress { get; set; }

    /// <summary>
    /// Gets or sets the subject line of the email.
    /// </summary>
    /// <remarks>
    /// Displayed as the email headline in the recipient's inbox.
    /// </remarks>
    public required string Subject { get; set; }

    /// <summary>
    /// Gets or sets the main body content of the email.
    /// </summary>
    /// <remarks>
    /// Contains the primary message content to be delivered to the recipient.
    /// May include plain text or HTML markup depending on the <see cref="ContentType"/> setting.
    /// </remarks>
    public required string Body { get; set; }

    /// <summary>
    /// Gets or sets the content type (plain text or HTML) of the email.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="EmailContentType.Plain"/>.
    /// Determines how email clients will render the body content.
    /// </remarks>
    public EmailContentType ContentType { get; set; } = EmailContentType.Plain;

    /// <summary>
    /// Gets or sets the policy defining when the email should be sent.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="SendingTimePolicy.Anytime"/> allowing delivery at any time.
    /// </remarks>
    public SendingTimePolicy SendingTimePolicy { get; set; } = SendingTimePolicy.Anytime;
}
