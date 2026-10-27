namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Identifies a recipient for a composed email notification by their email address
/// and the associated sending options, including SAS-referenced files.
/// </summary>
public class RecipientComposedEmail
{
    /// <summary>
    /// Gets or sets the email address of the recipient.
    /// </summary>
    public required string EmailAddress { get; set; }

    /// <summary>
    /// Gets or sets the email sending options, including the SAS-referenced files to include.
    /// </summary>
    public required ComposedEmailSendingOptions Settings { get; set; }
}
