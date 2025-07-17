using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;

namespace Altinn.Notifications.Mappers;

/// <summary>
/// Extension methods for mapping between external data transfer models and internal domain models
/// specifically for notification orders that are meant to be handled immediately (Instant Notification Orders).
/// </summary>
public static class InstantNotificationOrderMapper
{
    /// <summary>
    /// Maps an <see cref="InstantNotificationOrderTracking"/> domain model to an <see cref="InstantNotificationOrderResponseExt"/> response model.
    /// </summary>
    /// <param name="source">The tracking information for the instant notification order.</param>
    /// <returns>An <see cref="InstantNotificationOrderResponseExt"/> containing the order chain identifier and notification shipment details.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or its required properties are null.</exception>
    public static InstantNotificationOrderResponseExt MapToInstantNotificationOrderResponse(this InstantNotificationOrderTracking source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.Notification);

        return new InstantNotificationOrderResponseExt
        {
            OrderChainId = source.OrderChainId,
            Notification = new NotificationOrderChainShipmentExt
            {
                ShipmentId = source.Notification.ShipmentId,
                SendersReference = source.Notification.SendersReference
            }
        };
    }

    /// <summary>
    /// Maps an <see cref="InstantNotificationOrderRequestExt"/> external request model to an <see cref="InstantNotificationOrder"/> domain model.
    /// </summary>
    /// <param name="source">The external request model to map from.</param>
    /// <param name="creatorShortName">The short name of the creator of the notification.</param>
    /// <param name="created">The timestamp indicating when the instant notification order was created. This should be provided by the caller for improved testability.</param>
    /// <returns>An <see cref="InstantNotificationOrder"/> domain model.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or its required properties are null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="creatorShortName"/> is null or empty.</exception>
    public static InstantNotificationOrder MapToInstantNotificationOrder(this InstantNotificationOrderRequestExt source, string creatorShortName, DateTime created)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.InstantNotificationRecipient);
        ArgumentNullException.ThrowIfNull(source.InstantNotificationRecipient.ShortMessageDeliveryDetails);
        ArgumentNullException.ThrowIfNull(source.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent);

        ArgumentException.ThrowIfNullOrWhiteSpace(creatorShortName);

        return new InstantNotificationOrder
        {
            Created = created,
            OrderId = Guid.NewGuid(),
            OrderChainId = Guid.NewGuid(),
            IdempotencyId = source.IdempotencyId,
            Creator = new Creator(creatorShortName),
            SendersReference = source.SendersReference,
            InstantNotificationRecipient = source.InstantNotificationRecipient.MapToInstantNotificationRecipient()
        };
    }

    /// <summary>
    /// Maps a <see cref="ShortMessageContentExt"/> external content model to a <see cref="ShortMessageContent"/> domain model.
    /// </summary>
    /// <param name="source">The external content model to map from.</param>
    /// <returns>A <see cref="ShortMessageContent"/> domain model.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    private static ShortMessageContent MapToShortMessageContent(this ShortMessageContentExt source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new ShortMessageContent
        {
            Message = source.Body,
            Sender = source.Sender
        };
    }

    /// <summary>
    /// Maps a <see cref="ShortMessageDeliveryDetailsExt"/> external delivery details model to a <see cref="ShortMessageDeliveryDetails"/> domain model.
    /// </summary>
    /// <param name="source">The external delivery details model to map from.</param>
    /// <returns>A <see cref="ShortMessageDeliveryDetails"/> domain model.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or its required properties are null.</exception>
    private static ShortMessageDeliveryDetails MapToShortMessageDeliveryDetails(this ShortMessageDeliveryDetailsExt source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.ShortMessageContent);

        return new ShortMessageDeliveryDetails
        {
            PhoneNumber = source.PhoneNumber,
            TimeToLiveInSeconds = source.TimeToLiveInSeconds,
            ShortMessageContent = source.ShortMessageContent.MapToShortMessageContent()
        };
    }

    /// <summary>
    /// Maps an <see cref="InstantNotificationRecipientExt"/> external recipient model to an <see cref="InstantNotificationRecipient"/> domain model.
    /// </summary>
    /// <param name="source">The external recipient model to map from.</param>
    /// <returns>An <see cref="InstantNotificationRecipient"/> domain model.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or its required properties are null.</exception>
    private static InstantNotificationRecipient MapToInstantNotificationRecipient(this InstantNotificationRecipientExt source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.ShortMessageDeliveryDetails);

        return new InstantNotificationRecipient
        {
            ShortMessageDeliveryDetails = source.ShortMessageDeliveryDetails.MapToShortMessageDeliveryDetails()
        };
    }
}
