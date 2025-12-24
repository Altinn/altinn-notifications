using System.Diagnostics.CodeAnalysis;
using Altinn.Notifications.Core.Enums;

namespace StatusFeedBackfillTool;

/// <summary>
/// Configuration settings for the status feed backfill tool.
/// </summary>
[ExcludeFromCodeCoverage]
public class BackfillSettings
{
    /// <summary>
    /// Number of orders to process in each batch.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// If true, performs a dry run without actually inserting status feed entries.
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// Optional: Only backfill orders from this creator.
    /// If null or empty, backfills for all creators.
    /// </summary>
    public string? CreatorNameFilter { get; set; }

    /// <summary>
    /// Optional: Only backfill orders processed after this date.
    /// If null, uses the oldest status feed entry date.
    /// </summary>
    public DateTime? MinProcessedDate { get; set; }

    /// <summary>
    /// Optional: Filter by specific order processing status.
    /// Valid values: Registered, Processing, Completed, SendConditionNotMet, Cancelled, Processed.
    /// If null, processes all orders without status filter.
    /// </summary>
    public OrderProcessingStatus? OrderProcessingStatusFilter { get; set; }

    /// <summary>
    /// Optional: Specific list of order IDs to backfill.
    /// If provided, this takes precedence over all other filters.
    /// </summary>
    public List<Guid>? OrderIds { get; set; }
}
