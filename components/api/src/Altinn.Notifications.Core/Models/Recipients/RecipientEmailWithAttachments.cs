namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Defines a model for sending an email notification with file attachments to a specific email address.
/// </summary>
public class RecipientEmailWithAttachments
{
    /// <summary>
    /// Gets or sets the email address of the intended recipient.
    /// </summary>
    /// <remarks>
    /// This is the destination address where the email will be delivered.
    /// </remarks>
    public required string EmailAddress { get; set; }

    /// <summary>
    /// Gets or sets the email sending options, including the SAS-referenced files to include.
    /// </summary>
    public required ComposedEmailSendingOptions Settings { get; set; }
}
