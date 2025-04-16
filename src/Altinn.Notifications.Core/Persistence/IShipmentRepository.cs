using Altinn.Notifications.Core.Models.Delivery;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Interface defining repository operations for managing notification shipments.
/// </summary>
public interface IShipmentRepository
{
    /// <summary>
    /// Retrieves the delivery manifest for a specific shipment identified by its unique identifier and creator name.
    /// </summary>
    /// <param name="alternateid">The unique identifier of the shipment to retrieve.</param>
    /// <param name="creatorName">The name of the creator/owner who originated the shipment.</param>
    /// <param name="cancellationToken">A token for canceling the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation that, when completed, contains the 
    /// <see cref="IShipmentDeliveryManifest"/> object associated with the delivery manifest
    /// for a specific shipment, or <c>null</c> if no matching shipment is found.
    /// </returns>
    Task<IShipmentDeliveryManifest?> GetDeliveryManifestAsync(Guid alternateid, string creatorName, CancellationToken cancellationToken);
}
