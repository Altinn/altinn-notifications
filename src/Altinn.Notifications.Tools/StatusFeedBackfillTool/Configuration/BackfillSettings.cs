using System.Diagnostics.CodeAnalysis;

namespace StatusFeedBackfillTool.Configuration;

/// <summary>
/// Configuration settings for the status feed backfill service.
/// </summary>
[ExcludeFromCodeCoverage]
public class BackfillSettings
{
    /// <summary>
    /// File path for reading the list of order IDs to process (JSON format).
    /// </summary>
    public string OrderIdsFilePath { get; set; } = "affected-orders.json";

    /// <summary>
    /// If true, performs a dry run without actually inserting status feed entries.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
