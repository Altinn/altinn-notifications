using Altinn.Notifications.Core.Models.Delivery;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Interface defining a repository service for retrieving shipment delivery manifests and their associated deliverable entities.
/// </summary>
/// <remarks>
/// This repository interface abstracts the data access layer for shipment information, enabling clients to retrieve
/// detailed information about shipments and their associated deliverable entities (SMS, Email).
/// </remarks>
public interface IShipmentDeliveryManifestRepository
{
    /// <summary>
    /// Retrieves the delivery manifest for a specified shipment.
    /// </summary>
    /// <param name="alternateid">The shipment's unique identifier.</param>
    /// <param name="creatorName">The creator or owner of the shipment.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    /// A <see cref="INotificationDeliveryManifest"/> containing shipment details and status of associated deliveries, or <see langword="null"/> if not found.
    /// </returns>
    Task<INotificationDeliveryManifest?> GetDeliveryManifestAsync(Guid alternateid, string creatorName, CancellationToken cancellationToken);
}
