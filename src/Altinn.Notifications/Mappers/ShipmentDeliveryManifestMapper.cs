using System.Collections.Immutable;

using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Models.Delivery;

namespace Altinn.Notifications.Mappers;

/// <summary>
/// Provides extension methods for mapping domain models of <see cref="ShipmentDeliveryManifest"/>
/// objects to their corresponding external data transfer models of <see cref="ShipmentDeliveryManifestExt"/>.
/// </summary>
public static class ShipmentDeliveryManifestMapper
{
    /// <summary>
    /// Maps a <see cref="ShipmentDeliveryManifest"/> to a <see cref="ShipmentDeliveryManifestExt"/>
    /// </summary>
    /// <param name="manifest">The shipment delivery manifest to map.</param>
    /// <returns>A mapped external representation of the shipment delivery manifest.</returns>
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
    /// Maps a collection of <see cref="IDeliverableEntity"/> objects to a collection of <see cref="IDeliverableEntityExt"/> objects.
    /// </summary>
    /// <param name="entities">The deliverable entities to map.</param>
    /// <returns>An immutable list of mapped external deliverable entities.</returns>
    private static IImmutableList<IDeliverableEntityExt> MapDeliverableEntities(IImmutableList<IDeliverableEntity> entities)
    {
        return [.. entities.Select(MapDeliverableEntity)];
    }

    /// <summary>
    /// Maps a <see cref="IDeliverableEntity"/> to its corresponding <see cref="IDeliverableEntityExt"/> implementation.
    /// </summary>
    /// <param name="deliverableEntity">The deliverable entity to map.</param>
    /// <returns>The mapped external deliverable entity.</returns>
    private static IDeliverableEntityExt MapDeliverableEntity(IDeliverableEntity deliverableEntity)
    {
        return deliverableEntity switch
        {
            SmsDeliveryManifest smsManifest => new SmsDeliveryManifestExt
            {
                Status = smsManifest.Status,
                LastUpdate = smsManifest.LastUpdate,
                Destination = smsManifest.Destination,
                StatusDescription = smsManifest.StatusDescription,
            },

            EmailDeliveryManifest emailManifest => new EmailDeliveryManifestExt
            {
                Status = emailManifest.Status,
                LastUpdate = emailManifest.LastUpdate,
                Destination = emailManifest.Destination,
                StatusDescription = emailManifest.StatusDescription,
            },

            _ => throw new ArgumentException($"Unsupported deliverable entity type: {deliverableEntity.GetType().Name}", nameof(deliverableEntity))
        };
    }
}
