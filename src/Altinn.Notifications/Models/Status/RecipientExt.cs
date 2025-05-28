using Altinn.Notifications.Models.Delivery;

namespace Altinn.Notifications.Models.Status;

/// <summary>
/// Agnostic representation of the delivery manifest interface
/// 
/// </summary>
public class RecipientExt : IDeliveryManifestExt
{
    /// <summary>
    /// The recipient destination, supporting both email and SMS formats.
    /// </summary>
    public string Destination { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current status of the processing lifecycle.
    /// </summary>
    public ProcessingLifecycleExt Status { get; init; }

    /// <summary>
    /// Gets the date and time of the last update.
    /// </summary>
    public DateTime LastUpdate { get; init; }
}
