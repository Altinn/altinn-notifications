namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents email details including recipient, content, and delivery parameters.
/// </summary>
public record InstantEmailDetails
{
    /// <summary>
    /// The recipient's email address.
    /// </summary>
    public required string EmailAddress { get; init; }

    /// <summary>
    /// The content and sender information.
    /// </summary>
    public required InstantEmailContent EmailContent { get; init; }
}
