using Altinn.Notifications.Core.Models.Delivery;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Interface defining repository operations for accessing and managing deliverable entities and their tracking information.
/// </summary>
/// <remarks>
/// It serves as the data access abstraction for notification tracking,
/// providing methods to retrieve shipment delivery manifests and their associated deliverable entities.
/// </remarks>
public interface IDeliverableEntitiesRepository
{
    /// <summary>
    /// Retrieves the delivery manifest for a specific shipment identified by its unique identifier and creator name.
    /// </summary>
    /// <param name="alternateid">The unique identifier of the shipment to retrieve.</param>
    /// <param name="creatorName">The name of the creator/owner who originated the shipment.</param>
    /// <param name="cancellationToken">A token for canceling the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation that, when completed, contains the 
    /// <see cref="IShipmentDeliveryManifest"/> object with its collection of <see cref="IDeliverableEntity"/> instances,
    /// or <c>null</c> if no matching shipment is found.
    /// </returns>
    Task<IShipmentDeliveryManifest?> GetDeliveryManifestAsync(Guid alternateid, string creatorName, CancellationToken cancellationToken);
}
