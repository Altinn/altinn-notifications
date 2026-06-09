using System.Diagnostics.CodeAnalysis;

namespace Altinn.Notifications.Tools.RetryDeadDeliveryReports.Configuration;

/// <summary>
/// Settings for processing dead delivery reports
/// </summary>
[ExcludeFromCodeCoverage]
public class ProcessingSettings
{
    /// <summary>
    /// Gets or sets the starting ID for processing dead delivery reports
    /// </summary>
    public int FromId { get; set; }

    /// <summary>
    /// Gets or sets the ending ID for processing dead delivery reports
    /// </summary>
    public int ToId { get; set; }
}
