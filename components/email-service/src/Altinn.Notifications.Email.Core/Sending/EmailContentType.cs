namespace Altinn.Notifications.Email.Core.Sending;

/// <summary>
/// Enum describing different possible content types for an email.
/// </summary>
public enum EmailContentType
{
    /// <summary>
    /// The email should be in the form of plain text.
    /// </summary>
    Plain,

    /// <summary>
    /// The email should be in the form of HTML.
    /// </summary>
    Html
}
