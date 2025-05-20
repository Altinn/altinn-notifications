using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Delivery;

/// <summary>
/// Represents a status feed.
/// </summary>
public record StatusFeed
{
    /// <summary>
    /// The sequence number of the status feed entry per creator
    /// </summary>
    public required int SequenceNumber { get; set; }

    /// <summary>
    /// The string representation of the jsonb object stored in the status feed table
    /// </summary>
    public required string OrderStatus { get; set; }
}
