using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implements the <see cref="INotificationDeliveryManifestService"/> interface,
/// for retrieving the delivery manifest for notification orders.
/// </summary>
public class NotificationDeliveryManifestService : INotificationDeliveryManifestService
{
    private readonly IShipmentDeliveryManifestRepository _shipmentDeliveryManifestRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationDeliveryManifestService"/> class.
    /// </summary>
    public NotificationDeliveryManifestService(IShipmentDeliveryManifestRepository shipmentDeliveryManifestRepository)
    {
        _shipmentDeliveryManifestRepository = shipmentDeliveryManifestRepository;
    }

    /// <inheritdoc />
    public async Task<Result<INotificationDeliveryManifest, ServiceError>> GetDeliveryManifestAsync(Guid alternateId, string creatorName, CancellationToken cancellationToken)
    {
        var order =
            await _shipmentDeliveryManifestRepository.GetDeliveryManifestAsync(alternateId, creatorName, cancellationToken);

        if (order == null)
        {
            return new ServiceError(404, "Shipment not found.");
        }

        return (NotificationDeliveryManifest)order;
    }
}
