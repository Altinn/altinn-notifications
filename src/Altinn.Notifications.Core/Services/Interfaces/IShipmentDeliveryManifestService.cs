using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface defining a service for retrieving shipment delivery manifests and their associated deliverable entities.
/// </summary>
public interface IShipmentDeliveryManifestService
{
    /// <summary>
    /// Retrieves the delivery manifest for a specific shipment.
    /// </summary>
    /// <param name="alternateid">The unique identifier of the shipment to retrieve.</param>
    /// <param name="creatorName">The name of the creator/owner who originated the shipment.</param>
    /// <param name="cancellationToken">A token for canceling the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. When complete, the task contains a 
    /// <see cref="Result{IShipmentDeliveryManifest, ServiceError}"/> that is either:
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       A successful result containing the shipment delivery manifest and information about its deliverable entities.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       An error result with a <see cref="ServiceError"/> (such as a 404 status code) if the shipment is not found.
    ///     </description>
    ///   </item>
    /// </list>
    /// </returns>
    Task<Result<IShipmentDeliveryManifest, ServiceError>> GetDeliveryManifestAsync(Guid alternateid, string creatorName, CancellationToken cancellationToken);
}
