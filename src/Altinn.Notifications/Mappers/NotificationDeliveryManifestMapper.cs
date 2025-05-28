using System.Collections.Immutable;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Models.Delivery;
using Altinn.Notifications.Models.Status;

namespace Altinn.Notifications.Mappers;

/// <summary>
/// Provides mapping functionality between domain models and their corresponding external data transfer models.
/// </summary>
public static class NotificationDeliveryManifestMapper
{
    /// <summary>
    /// Maps a domain notification delivery manifest to its external representation for API responses.
    /// </summary>
    /// <param name="manifest">The internal domain notification delivery manifest to transform.</param>
    /// <returns>
    /// An <see cref="INotificationDeliveryManifestExt"/> containing the mapped data from the original 
    /// domain manifest, ready for serialization and transmission to external clients.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the manifest parameter is null.</exception>
    public static INotificationDeliveryManifestExt MapToNotificationDeliveryManifestExt(this INotificationDeliveryManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return new NotificationDeliveryManifestExt
        {
            Type = manifest.Type,
            LastUpdate = manifest.LastUpdate,
            ShipmentId = manifest.ShipmentId,
            SendersReference = manifest.SendersReference,
            Status = MapProcessingLifecycle(manifest.Status),
            Recipients = manifest.Recipients?.MapToDeliveryManifestExtObjects() ?? []
        };
    }

    /// <summary>
    /// Maps a <see cref="ProcessingLifecycle"/> enum value to its corresponding <see cref="ProcessingLifecycleExt"/> value.
    /// </summary>
    /// <param name="status">The internal processing lifecycle status to map.</param>
    /// <returns>The corresponding external processing lifecycle status.</returns>
    internal static ProcessingLifecycleExt MapProcessingLifecycle(ProcessingLifecycle status)
    {
        return status switch
        {
            // Order statuses
            ProcessingLifecycle.Order_Completed => ProcessingLifecycleExt.Order_Completed,
            ProcessingLifecycle.Order_Cancelled => ProcessingLifecycleExt.Order_Cancelled,
            ProcessingLifecycle.Order_Registered => ProcessingLifecycleExt.Order_Registered,
            ProcessingLifecycle.Order_Processing => ProcessingLifecycleExt.Order_Processing,
            ProcessingLifecycle.Order_SendConditionNotMet => ProcessingLifecycleExt.Order_SendConditionNotMet,

            // SMS statuses
            ProcessingLifecycle.SMS_New => ProcessingLifecycleExt.SMS_New,
            ProcessingLifecycle.SMS_Failed => ProcessingLifecycleExt.SMS_Failed,
            ProcessingLifecycle.SMS_Sending => ProcessingLifecycleExt.SMS_Sending,
            ProcessingLifecycle.SMS_Accepted => ProcessingLifecycleExt.SMS_Accepted,
            ProcessingLifecycle.SMS_Delivered => ProcessingLifecycleExt.SMS_Delivered,
            ProcessingLifecycle.SMS_Failed_Deleted => ProcessingLifecycleExt.SMS_Failed_Deleted,
            ProcessingLifecycle.SMS_Failed_Expired => ProcessingLifecycleExt.SMS_Failed_Expired,
            ProcessingLifecycle.SMS_Failed_Rejected => ProcessingLifecycleExt.SMS_Failed_Rejected,
            ProcessingLifecycle.SMS_Failed_Undelivered => ProcessingLifecycleExt.SMS_Failed_Undelivered,
            ProcessingLifecycle.SMS_Failed_BarredReceiver => ProcessingLifecycleExt.SMS_Failed_BarredReceiver,
            ProcessingLifecycle.SMS_Failed_InvalidRecipient => ProcessingLifecycleExt.SMS_Failed_InvalidRecipient,
            ProcessingLifecycle.SMS_Failed_RecipientReserved => ProcessingLifecycleExt.SMS_Failed_RecipientReserved,
            ProcessingLifecycle.SMS_Failed_RecipientNotIdentified => ProcessingLifecycleExt.SMS_Failed_RecipientNotIdentified,

            // Email statuses
            ProcessingLifecycle.Email_New => ProcessingLifecycleExt.Email_New,
            ProcessingLifecycle.Email_Failed => ProcessingLifecycleExt.Email_Failed,
            ProcessingLifecycle.Email_Sending => ProcessingLifecycleExt.Email_Sending,
            ProcessingLifecycle.Email_Succeeded => ProcessingLifecycleExt.Email_Succeeded,
            ProcessingLifecycle.Email_Delivered => ProcessingLifecycleExt.Email_Delivered,
            ProcessingLifecycle.Email_Failed_Bounced => ProcessingLifecycleExt.Email_Failed_Bounced,
            ProcessingLifecycle.Email_Failed_Quarantined => ProcessingLifecycleExt.Email_Failed_Quarantined,
            ProcessingLifecycle.Email_Failed_FilteredSpam => ProcessingLifecycleExt.Email_Failed_FilteredSpam,
            ProcessingLifecycle.Email_Failed_InvalidFormat => ProcessingLifecycleExt.Email_Failed_InvalidFormat,
            ProcessingLifecycle.Email_Failed_TransientError => ProcessingLifecycleExt.Email_Failed_TransientError,
            ProcessingLifecycle.Email_Failed_RecipientReserved => ProcessingLifecycleExt.Email_Failed_RecipientReserved,
            ProcessingLifecycle.Email_Failed_SuppressedRecipient => ProcessingLifecycleExt.Email_Failed_SuppressedRecipient,
            ProcessingLifecycle.Email_Failed_RecipientNotIdentified => ProcessingLifecycleExt.Email_Failed_RecipientNotIdentified,

            // In case a new status is added to the enum but not to this mapping:
            _ => throw new ArgumentOutOfRangeException(nameof(status), $"Unsupported status: {status}")
        };
    }

    /// <summary>
    /// Maps a domain delivery manifest to its external representation for API responses.
    /// </summary>
    /// <param name="deliveryManifest">The domain delivery manifest to transform.</param>
    /// <returns>
    /// An <see cref="IDeliveryManifestExt"/> containing the mapped data from the domain
    /// manifest, ready for serialization and transmission to external clients.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the input entity is not recognized as a valid delivery manifest type (e.g., neither SMS nor email).
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when the manifest parameter is null.</exception>
    private static IDeliveryManifestExt MapToDeliveryManifestExt(IDeliveryManifest deliveryManifest)
    {
        ArgumentNullException.ThrowIfNull(deliveryManifest);

        return deliveryManifest switch
        {
            SmsDeliveryManifest smsManifest => smsManifest.MapToSmsDeliveryManifestExt(),

            EmailDeliveryManifest emailManifest => emailManifest.MapToEmailDeliveryManifestExt(),

            _ => throw new ArgumentException($"Unsupported delivery manifest type: {deliveryManifest.GetType().Name}", nameof(deliveryManifest))
        };
    }

    /// <summary>
    /// Maps a domain SMS delivery manifest to its external representation for API responses.
    /// </summary>
    /// <param name="manifest">The internal domain SMS delivery manifest to transform.</param>
    /// <returns>
    /// A <see cref="SmsDeliveryManifestExt"/> containing the mapped data from the domain
    /// manifest, ready for serialization and transmission to external clients.
    /// </returns>
    private static SmsDeliveryManifestExt MapToSmsDeliveryManifestExt(this SmsDeliveryManifest manifest)
    {
        return new SmsDeliveryManifestExt
        {
            LastUpdate = manifest.LastUpdate,
            Destination = manifest.Destination,
            Status = MapProcessingLifecycle(manifest.Status)
        };
    }

    /// <summary>
    /// Maps a domain email delivery manifest to its external representation for API responses.
    /// </summary>
    /// <param name="manifest">The internal domain email delivery manifest to transform.</param>
    /// <returns>
    /// A <see cref="EmailDeliveryManifestExt"/> containing the mapped data from the domain
    /// manifest, ready for serialization and transmission to external clients.
    /// </returns>
    private static EmailDeliveryManifestExt MapToEmailDeliveryManifestExt(this EmailDeliveryManifest manifest)
    {
        return new EmailDeliveryManifestExt
        {
            LastUpdate = manifest.LastUpdate,
            Destination = manifest.Destination,
            Status = MapProcessingLifecycle(manifest.Status)
        };
    }

    /// <summary>
    /// Maps a collection of domain delivery manifest to their corresponding external representations.
    /// </summary>
    /// <param name="entities">The collection of internal domain delivery manifest to transform.</param>
    /// <returns>
    /// An immutable list of external delivery manifests, each converted to its appropriate type-specific implementation,
    /// or an empty collection if no entities are present.
    /// </returns>
    public static IImmutableList<IDeliveryManifestExt> MapToDeliveryManifestExtObjects(this IImmutableList<IDeliveryManifest> entities)
    {
        return entities.Count > 0 ? [.. entities.Select(MapToDeliveryManifestExt)] : [];
    }
}
