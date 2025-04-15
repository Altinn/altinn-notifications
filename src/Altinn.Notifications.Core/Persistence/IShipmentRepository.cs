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
    /// <param name="shipmentId">The unique identifier of the shipment to retrieve.</param>
    /// <param name="creatorName">The name of the creator/owner who originated the shipment.</param>
    /// <returns>
    /// A task representing the asynchronous operation that, when completed, contains the 
    /// <see cref="IShipmentDeliveryManifest"/> object associated with the specified shipment identifier,
    /// or <c>null</c> if no matching shipment is found.
    /// </returns>
    Task<IShipmentDeliveryManifest?> GetDeliveryManifest(Guid shipmentId, string creatorName);
}
