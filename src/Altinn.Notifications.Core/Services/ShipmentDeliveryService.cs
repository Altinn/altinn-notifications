using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Testing class for the <see cref="IShipmentDeliveryService"/> interface.
/// </summary>
/// <seealso cref="IShipmentDeliveryService" />
public class ShipmentDeliveryService : IShipmentDeliveryService
{
    private readonly IShipmentRepository _shipmentRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetOrderService"/> class.
    /// </summary>
    public ShipmentDeliveryService(IShipmentRepository shipmentRepository)
    {
        _shipmentRepository = shipmentRepository;
    }

    /// <inheritdoc />
    public async Task<Result<IShipmentDeliveryManifest, ServiceError>> GetDeliveryManifest(Guid shipmentId, string creatorName)
    {
        var order = (ShipmentDeliveryManifest?)await _shipmentRepository.GetDeliveryManifest(shipmentId, creatorName);

        if (order == null)
        {
            return new ServiceError(404);
        }

        return order;
    }
}
