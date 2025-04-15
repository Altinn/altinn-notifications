using System.Collections.Immutable;

using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Models.Delivery;

namespace Altinn.Notifications.Mappers;

/// <summary>
/// Provides mapping functionality between domain models and their corresponding external data transfer models.
/// </summary>
/// <remarks>
/// This mapper handles the transformation of internal <see cref="IShipmentDeliveryManifest"/> objects 
/// to their external API representation as <see cref="IShipmentDeliveryManifestExt"/> objects.
/// </remarks>
public static class ShipmentDeliveryManifestMapper
{
    /// <summary>
    /// Maps a domain shipment delivery manifest to its external representation.
    /// </summary>
    /// <param name="manifest">The domain shipment delivery manifest to map.</param>
    /// <returns>The mapped external representation of the shipment delivery manifest.</returns>
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
            Recipients = MapDeliverableEntities(manifest.Recipients)
        };
    }

    /// <summary>
    /// Maps a collection of domain deliverable entities to their external representations.
    /// </summary>
    /// <param name="entities">The domain deliverable entities to map.</param>
    /// <returns>An immutable list of mapped external deliverable entities.</returns>
    private static IImmutableList<IDeliverableEntityExt> MapDeliverableEntities(IImmutableList<IDeliverableEntity> entities)
    {
        return entities.Count == 0 ? [] : [.. entities.Select(MapDeliverableEntity)];
    }

    /// <summary>
    /// Maps a domain deliverable entity to its appropriate external representation based on its concrete type.
    /// </summary>
    /// <param name="deliverableEntity">The domain deliverable entity to map.</param>
    /// <returns>The mapped external deliverable entity.</returns>
    /// <exception cref="ArgumentException">Thrown if the deliverable entity is of an unsupported type.</exception>
    private static IDeliverableEntityExt MapDeliverableEntity(IDeliverableEntity deliverableEntity)
    {
        return deliverableEntity switch
        {
            SmsDeliveryManifest smsManifest => MapSmsDeliveryManifest(smsManifest),

            EmailDeliveryManifest emailManifest => MapEmailDeliveryManifest(emailManifest),

            _ => throw new ArgumentException($"Unsupported deliverable entity type: {deliverableEntity.GetType().Name}", nameof(deliverableEntity))
        };
    }

    /// <summary>
    /// Maps an SMS delivery manifest to its external representation.
    /// </summary>
    /// <param name="manifest">The SMS delivery manifest to map.</param>
    /// <returns>The mapped external SMS delivery manifest.</returns>
    private static SmsDeliveryManifestExt MapSmsDeliveryManifest(SmsDeliveryManifest manifest)
    {
        return new SmsDeliveryManifestExt
        {
            Status = manifest.Status,
            LastUpdate = manifest.LastUpdate,
            Destination = manifest.Destination,
            StatusDescription = manifest.StatusDescription,
        };
    }

    /// <summary>
    /// Maps an email delivery manifest to its external representation.
    /// </summary>
    /// <param name="manifest">The email delivery manifest to map.</param>
    /// <returns>The mapped external email delivery manifest.</returns>
    private static EmailDeliveryManifestExt MapEmailDeliveryManifest(EmailDeliveryManifest manifest)
    {
        return new EmailDeliveryManifestExt
        {
            Status = manifest.Status,
            LastUpdate = manifest.LastUpdate,
            Destination = manifest.Destination,
            StatusDescription = manifest.StatusDescription,
        };
    }
}
