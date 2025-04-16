using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implements the <see cref="IShipmentDeliveryManifestService"/> interface,
/// for retrieving shipment delivery manifests and their associated deliverable entities.
/// </summary>
/// <remarks>
/// This service is responsible for fetching detailed delivery manifests for shipments,
/// including current status and tracking information of deliverable entities such as Email and SMS.
/// It acts as an abstraction layer between controllers and data repositories.
/// </remarks>
public class ShipmentDeliveryManifestService : IShipmentDeliveryManifestService
{
    private readonly IShipmentDeliveryManifestRepository _shipmentDeliveryManifestRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipmentDeliveryManifestService"/> class.
    /// </summary>
    public ShipmentDeliveryManifestService(IShipmentDeliveryManifestRepository shipmentDeliveryManifestRepository)
    {
        _shipmentDeliveryManifestRepository = shipmentDeliveryManifestRepository;
    }

    /// <inheritdoc />
    public async Task<Result<IShipmentDeliveryManifest, ServiceError>> GetDeliveryManifestAsync(Guid alternateid, string creatorName, CancellationToken cancellationToken)
    {
        var order =
            await _shipmentDeliveryManifestRepository.GetDeliveryManifestAsync(alternateid, creatorName, cancellationToken);

        if (order == null)
        {
            return new ServiceError(404, "Shipment not found.");
        }

        return (ShipmentDeliveryManifest)order;
    }
}
