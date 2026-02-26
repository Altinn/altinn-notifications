namespace Altinn.Notifications.Core.Models.Status;

/// <summary>
/// Represents a status feed.
/// </summary>
public record StatusFeed
{
    /// <summary>
    /// The sequence number id of the status feed entry 
    /// </summary>
    public required long SequenceNumber { get; set; }

    /// <summary>
    /// The OrderStatus of the status feed entry <see cref="OrderStatus"/>.
    /// </summary>
    public required OrderStatus OrderStatus { get; set; }
}
