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
    private readonly INotificationDeliveryManifestRepository _notificationDeliveryManifestRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationDeliveryManifestService"/> class.
    /// </summary>
    public NotificationDeliveryManifestService(INotificationDeliveryManifestRepository notificationDeliveryManifestRepository)
    {
        _notificationDeliveryManifestRepository = notificationDeliveryManifestRepository;
    }

    /// <inheritdoc />
    public async Task<Result<INotificationDeliveryManifest, ServiceError>> GetDeliveryManifestAsync(Guid alternateId, string creatorName, CancellationToken cancellationToken)
    {
        var deliveryManifest = await _notificationDeliveryManifestRepository.GetDeliveryManifestAsync(alternateId, creatorName, cancellationToken);

        return deliveryManifest is NotificationDeliveryManifest manifest ? manifest : new ServiceError(404, "Shipment not found.", "shipment-not-found");
    }
}
