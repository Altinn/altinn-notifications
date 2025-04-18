using System.Collections.Immutable;

using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Models.Delivery;

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
    public static INotificationDeliveryManifestExt MapToNotificationDeliveryManifestExt(this INotificationDeliveryManifest manifest)
    {
        return new NotificationDeliveryManifestExt
        {
            Type = manifest.Type,
            Status = manifest.Status,
            LastUpdate = manifest.LastUpdate,
            ShipmentId = manifest.ShipmentId,
            SendersReference = manifest.SendersReference,
            StatusDescription = manifest.StatusDescription,
            Recipients = manifest.Recipients.MapToDeliveryManifestExtObjects()
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
    private static IDeliveryManifestExt MapToDeliveryManifestExt(IDeliveryManifest deliveryManifest)
    {
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
    /// <param name="smsDeliveryManifest">The internal domain SMS delivery manifest to transform.</param>
    /// <returns>
    /// A <see cref="SmsDeliveryManifestExt"/> containing the mapped data from the domain
    /// manifest, ready for serialization and transmission to external clients.
    /// </returns>
    private static SmsDeliveryManifestExt MapToSmsDeliveryManifestExt(this SmsDeliveryManifest smsDeliveryManifest)
    {
        return new SmsDeliveryManifestExt
        {
            Status = smsDeliveryManifest.Status,
            LastUpdate = smsDeliveryManifest.LastUpdate,
            Destination = smsDeliveryManifest.Destination,
            StatusDescription = smsDeliveryManifest.StatusDescription
        };
    }

    /// <summary>
    /// Maps a domain email delivery manifest to its external representation for API responses.
    /// </summary>
    /// <param name="emailDeliveryManifest">The internal domain email delivery manifest to transform.</param>
    /// <returns>
    /// A <see cref="EmailDeliveryManifestExt"/> containing the mapped data from the domain
    /// manifest, ready for serialization and transmission to external clients.
    /// </returns>
    private static EmailDeliveryManifestExt MapToEmailDeliveryManifestExt(this EmailDeliveryManifest emailDeliveryManifest)
    {
        return new EmailDeliveryManifestExt
        {
            Status = emailDeliveryManifest.Status,
            LastUpdate = emailDeliveryManifest.LastUpdate,
            Destination = emailDeliveryManifest.Destination,
            StatusDescription = emailDeliveryManifest.StatusDescription
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
    private static IImmutableList<IDeliveryManifestExt> MapToDeliveryManifestExtObjects(this IImmutableList<IDeliveryManifest> entities)
    {
        return entities.Count > 0 ? [.. entities.Select(MapToDeliveryManifestExt)] : [];
    }
}
