using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface defining a service for retrieving the delivery manifest for notification orders.
/// </summary>
public interface INotificationDeliveryManifestService
{
    /// <summary>
    /// Retrieves the delivery manifest for a specific notification order.
    /// </summary>
    /// <param name="alternateId">The unique alternate identifier of the notification order.</param>
    /// <param name="creatorName">The name of the creator/owner who originated the notification order.</param>
    /// <param name="cancellationToken">A token for canceling the asynchronous operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. When complete, the task contains a 
    /// <see cref="Result{INotificationDeliveryManifest, ServiceError}"/> that is either:
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       A successful result containing the delivery manifest
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       An error result with a <see cref="ServiceError"/> (such as a 404 status code) if the notification order is not found
    ///     </description>
    ///   </item>
    /// </list>
    /// </returns>
    Task<Result<INotificationDeliveryManifest, ServiceError>> GetDeliveryManifestAsync(Guid alternateId, string creatorName, CancellationToken cancellationToken);
}
