using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Errors;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Caching.Memory;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implements the <see cref="INotificationDeliveryManifestService"/> interface,
/// for retrieving the delivery manifest for notification orders.
/// </summary>
public class NotificationDeliveryManifestService : INotificationDeliveryManifestService
{
    private static readonly TimeSpan _cacheSlidingExpiration = TimeSpan.FromHours(1);

    private static readonly HashSet<ProcessingLifecycle> _terminalStatuses =
    [
        ProcessingLifecycle.Order_Completed,
        ProcessingLifecycle.Order_SendConditionNotMet,
        ProcessingLifecycle.Order_Cancelled,
        ProcessingLifecycle.Order_Failed,
        ProcessingLifecycle.SMS_Delivered,
        ProcessingLifecycle.SMS_Failed,
        ProcessingLifecycle.SMS_Failed_InvalidRecipient,
        ProcessingLifecycle.SMS_Failed_RecipientReserved,
        ProcessingLifecycle.SMS_Failed_BarredReceiver,
        ProcessingLifecycle.SMS_Failed_Deleted,
        ProcessingLifecycle.SMS_Failed_Expired,
        ProcessingLifecycle.SMS_Failed_Undelivered,
        ProcessingLifecycle.SMS_Failed_RecipientNotIdentified,
        ProcessingLifecycle.SMS_Failed_TTL,
        ProcessingLifecycle.SMS_Failed_Rejected,
        ProcessingLifecycle.SMS_Failed_ContentTooLarge,
        ProcessingLifecycle.Email_Delivered,
        ProcessingLifecycle.Email_Failed,
        ProcessingLifecycle.Email_Failed_RecipientReserved,
        ProcessingLifecycle.Email_Failed_RecipientNotIdentified,
        ProcessingLifecycle.Email_Failed_InvalidFormat,
        ProcessingLifecycle.Email_Failed_SuppressedRecipient,
        ProcessingLifecycle.Email_Failed_TransientError,
        ProcessingLifecycle.Email_Failed_Bounced,
        ProcessingLifecycle.Email_Failed_FilteredSpam,
        ProcessingLifecycle.Email_Failed_Quarantined,
        ProcessingLifecycle.Email_Failed_TTL,
        ProcessingLifecycle.Email_Failed_InvalidSasUrl,
        ProcessingLifecycle.Email_Failed_PayloadTooLarge,
        ProcessingLifecycle.Email_Failed_ContentTooLarge
    ];

    private readonly INotificationDeliveryManifestRepository _notificationDeliveryManifestRepository;
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationDeliveryManifestService"/> class.
    /// </summary>
    public NotificationDeliveryManifestService(
        INotificationDeliveryManifestRepository notificationDeliveryManifestRepository,
        IMemoryCache memoryCache)
    {
        _notificationDeliveryManifestRepository = notificationDeliveryManifestRepository;
        _memoryCache = memoryCache;
    }

    /// <inheritdoc />
    public async Task<Result<INotificationDeliveryManifest>> GetDeliveryManifestAsync(Guid alternateId, string creatorName, CancellationToken cancellationToken)
    {
        string cacheKey = CreateCacheKey(alternateId, creatorName);

        if (_memoryCache.TryGetValue(cacheKey, out NotificationDeliveryManifest? cachedManifest) && cachedManifest is not null)
        {
            return cachedManifest;
        }

        var deliveryManifest = await _notificationDeliveryManifestRepository.GetDeliveryManifestAsync(alternateId, creatorName, cancellationToken);

        if (deliveryManifest is NotificationDeliveryManifest manifest)
        {
            if (CanCacheManifest(manifest))
            {
                _memoryCache.Set(
                    cacheKey,
                    manifest,
                    new MemoryCacheEntryOptions
                    {
                        SlidingExpiration = _cacheSlidingExpiration
                    });
            }

            return manifest;
        }

        return Problems.ShipmentNotFound;
    }

    /// <summary>
    /// Creates a cache key for a shipment manifest lookup.
    /// </summary>
    /// <param name="alternateId">The shipment alternate identifier.</param>
    /// <param name="creatorName">The shipment creator name.</param>
    /// <returns>A unique cache key for the given shipment context.</returns>
    private static string CreateCacheKey(Guid alternateId, string creatorName)
    {
        return $"{alternateId}:{creatorName}";
    }

    /// <summary>
    /// Determines whether a manifest can be cached.
    /// </summary>
    /// <param name="manifest">The manifest to evaluate.</param>
    /// <returns><see langword="true"/> when order and recipients are terminal; otherwise <see langword="false"/>.</returns>
    private static bool CanCacheManifest(NotificationDeliveryManifest manifest)
    {
        return IsTerminalStatus(manifest.Status) &&
               manifest.Recipients.All(recipient => IsTerminalStatus(recipient.Status));
    }

    /// <summary>
    /// Determines whether a processing status is terminal.
    /// </summary>
    /// <param name="status">The status to evaluate.</param>
    /// <returns><see langword="true"/> when the status is terminal; otherwise <see langword="false"/>.</returns>
    private static bool IsTerminalStatus(ProcessingLifecycle status)
    {
        return _terminalStatuses.Contains(status);
    }
}
