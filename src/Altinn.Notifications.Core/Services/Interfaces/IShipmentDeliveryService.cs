using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Testing interface.
/// </summary>
public interface IShipmentDeliveryService
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
    public Task<Result<IShipmentDeliveryManifest, ServiceError>> GetDeliveryManifest(Guid shipmentId, string creatorName);
}
