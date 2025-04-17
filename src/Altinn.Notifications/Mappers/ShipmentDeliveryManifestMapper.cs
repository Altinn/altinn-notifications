using System.Collections.Immutable;

using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Models.Delivery;

namespace Altinn.Notifications.Mappers;

/// <summary>
/// Provides mapping functionality between domain models and their corresponding external data transfer models.
/// </summary>
/// <remarks>
/// This mapper serves as a critical bridge between the internal domain models and the external API-facing models.
/// </remarks>
public static class ShipmentDeliveryManifestMapper
{
    /// <summary>
    /// Maps a domain shipment delivery manifest to its external representation for API responses.
    /// </summary>
    /// <param name="manifest">The internal domain shipment delivery manifest to transform.</param>
    /// <returns>
    /// An <see cref="IShipmentDeliveryManifestExt"/> containing the mapped data from the original 
    /// domain manifest, ready for serialization and transmission to external clients.
    /// </returns>
    public static IShipmentDeliveryManifestExt MapToShipmentDeliveryManifestExt(this IShipmentDeliveryManifest manifest)
    {
        return new ShipmentDeliveryManifestExt
        {
            Type = manifest.Type,
            Status = manifest.Status,
            LastUpdate = manifest.LastUpdate,
            ShipmentId = manifest.ShipmentId,
            SendersReference = manifest.SendersReference,
            StatusDescription = manifest.StatusDescription,
            Recipients = manifest.Recipients.MapToDeliverableEntitiesExt()
        };
    }

    /// <summary>
    /// Maps a domain deliverable entity to its channel-specific external representation.
    /// </summary>
    /// <param name="deliverableEntity">The internal domain deliverable entity to transform.</param>
    /// <returns>
    /// An appropriate implementation of <see cref="IDeliveryStatusInfoExt"/> based on the concrete type
    /// of the input entity, preserving all tracking and addressing information.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the input entity is not a recognized deliverable type (neither SMS nor email).
    /// </exception>
    private static IDeliveryStatusInfoExt MapToDeliverableEntityExt(IDeliverableEntity deliverableEntity)
    {
        return deliverableEntity switch
        {
            SmsDeliveryManifest smsManifest => smsManifest.MapToSmsDeliveryManifestExt(),

            EmailDeliveryManifest emailManifest => emailManifest.MapToEmailDeliveryManifestExt(),

            _ => throw new ArgumentException($"Unsupported deliverable entity type: {deliverableEntity.GetType().Name}", nameof(deliverableEntity))
        };
    }

    /// <summary>
    /// Maps an SMS delivery manifest from the domain model to its external representation.
    /// </summary>
    /// <param name="manifest">The internal domain SMS delivery manifest to transform.</param>
    /// <returns>
    /// A <see cref="SmsDeliveryManifestExt"/> containing the mapped notification tracking data 
    /// for SMS delivery, ready for serialization as part of an API response.
    /// </returns>
    private static SmsDeliveryManifestExt MapToSmsDeliveryManifestExt(this SmsDeliveryManifest manifest)
    {
        return new SmsDeliveryManifestExt
        {
            Status = manifest.Status,
            LastUpdate = manifest.LastUpdate,
            Destination = manifest.Destination,
            StatusDescription = manifest.StatusDescription
        };
    }

    /// <summary>
    /// Maps an email delivery manifest from the domain model to its external representation.
    /// </summary>
    /// <param name="manifest">The internal domain email delivery manifest to transform.</param>
    /// <returns>
    /// A <see cref="EmailDeliveryManifestExt"/> containing the mapped notification tracking data 
    /// for email delivery, ready for serialization as part of an API response.
    /// </returns>
    private static EmailDeliveryManifestExt MapToEmailDeliveryManifestExt(this EmailDeliveryManifest manifest)
    {
        return new EmailDeliveryManifestExt
        {
            Status = manifest.Status,
            LastUpdate = manifest.LastUpdate,
            Destination = manifest.Destination,
            StatusDescription = manifest.StatusDescription,
        };
    }

    /// <summary>
    /// Maps a collection of domain deliverable entities to their corresponding external representations.
    /// </summary>
    /// <param name="entities">The collection of internal domain deliverable entities to transform.</param>
    /// <returns>
    /// An immutable list of external deliverable entities, each converted to its appropriate type-specific implementation,
    /// or an empty collection if no entities are present.
    /// </returns>
    private static IImmutableList<IDeliveryStatusInfoExt> MapToDeliverableEntitiesExt(this IImmutableList<IDeliverableEntity> entities)
    {
        return entities.Count == 0 ? [] : [.. entities.Select(MapToDeliverableEntityExt)];
    }
}
