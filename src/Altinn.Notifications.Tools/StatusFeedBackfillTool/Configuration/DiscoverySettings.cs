using System.Diagnostics.CodeAnalysis;
using Altinn.Notifications.Core.Enums;

namespace StatusFeedBackfillTool.Configuration;

/// <summary>
/// Configuration settings for the order discovery service.
/// </summary>
[ExcludeFromCodeCoverage]
public class DiscoverySettings
{
    /// <summary>
    /// File path for storing the list of discovered order IDs (JSON format).
    /// </summary>
    public string OrderIdsFilePath { get; set; } = "affected-orders.json";

    /// <summary>
    /// Maximum number of orders to retrieve from the discovery query.
    /// Acts as a safety limit to prevent processing too many orders at once.
    /// </summary>
    public int MaxOrders { get; set; } = 100;

    /// <summary>
    /// Optional: Only discover orders from this creator.
    /// If null or empty, discovers for all creators.
    /// </summary>
    public string? CreatorNameFilter { get; set; }

    /// <summary>
    /// Optional: Only discover orders processed after this timestamp (date and time).
    /// If null, uses the oldest status feed entry date.
    /// </summary>
    public DateTime? MinProcessedDateTimeFilter { get; set; }

    /// <summary>
    /// Optional: Filter by specific order processing status.
    /// Only final statuses are valid: Completed, SendConditionNotMet.
    /// Processing and other non-final statuses are automatically excluded from discovery.
    /// If null, discovers orders with any final status.
    /// </summary>
    public OrderProcessingStatus? OrderProcessingStatusFilter { get; set; }
}
