namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Groups recipients associated with the same resource identifier for coordinated notification processing.
/// </summary>
public class ResourceBoundRecipients
{
    /// <summary>
    /// Gets or sets the resource identifier that this group of recipients is associated with.
    /// </summary>
    /// <value>
    /// The resource identifier.
    /// </value>
    public string? ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the collection of recipients associated with this resource.
    /// </summary>
    /// <value>
    /// The collection of recipients.
    /// </value>
    public required List<Recipient> Recipients { get; set; } = [];
}
