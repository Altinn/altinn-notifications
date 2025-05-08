using Altinn.Notifications.Core.Models.Delivery;

namespace Altinn.Notifications.Core.Persistence;

/// <summary>
/// Interface defining a repository service for retrieving the delivery manifest for notification orders.
/// </summary>
public interface INotificationDeliveryManifestRepository
{
    /// <summary>
    /// Retrieves the delivery manifest for a specific notification order from the data store.
    /// </summary>
    /// <param name="alternateId">The unique alternate identifier of the notification order.</param>
    /// <param name="creatorName">The name of the creator/owner who originated the notification order.</param>
    /// <param name="cancellationToken">A token for canceling the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="INotificationDeliveryManifest"/> containing the delivery manifest, or <see langword="null"/> if the notification order was not found.
    /// </returns>
    Task<INotificationDeliveryManifest?> GetDeliveryManifestAsync(Guid alternateId, string creatorName, CancellationToken cancellationToken);
}
