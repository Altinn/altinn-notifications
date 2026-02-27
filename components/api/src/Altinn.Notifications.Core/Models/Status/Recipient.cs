using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Delivery;

namespace Altinn.Notifications.Core.Models.Status;

/// <summary>
/// Agnostic representation of the delivery manifest interface
/// 
/// </summary>
public class Recipient : IDeliveryManifest
{
    /// <summary>
    /// The recipient destination, supporting both email and SMS formats.
    /// </summary>
    public string Destination { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current status of the processing lifecycle.
    /// </summary>
    public ProcessingLifecycle Status { get; init; }

    /// <summary>
    /// Gets the date and time of the last update.
    /// </summary>
    public DateTime LastUpdate { get; init; }
}
