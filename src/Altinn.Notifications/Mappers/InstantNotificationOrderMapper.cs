using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Models.ShortMessageService;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;

namespace Altinn.Notifications.Mappers;

/// <summary>
/// Mapper class for converting between instant notification order request models and domain models.
/// </summary>
public static class InstantNotificationOrderMapper
{
    /// <summary>
    /// Maps from an instant notification order domain model to a short message for SMS delivery.
    /// </summary>
    /// <param name="source">The instant notification order to map from.</param>
    /// <param name="defaultSenderNumber">The default sender number.</param>
    /// <returns>A short message configured for SMS delivery.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or any of its required properties are null.</exception>
    public static ShortMessage MapToShortMessage(this InstantNotificationOrder source, string defaultSenderNumber)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.InstantNotificationRecipient);
        ArgumentNullException.ThrowIfNull(source.InstantNotificationRecipient.ShortMessageDeliveryDetails);
        ArgumentNullException.ThrowIfNull(source.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent);

        return new ShortMessage
        {
            NotificationId = source.OrderId,
            Recipient = source.InstantNotificationRecipient.ShortMessageDeliveryDetails.PhoneNumber,
            TimeToLive = source.InstantNotificationRecipient.ShortMessageDeliveryDetails.TimeToLiveInSeconds,
            Message = source.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Message,
            Sender = source.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Sender ?? defaultSenderNumber,
        };
    }

    /// <summary>
    /// Maps an <see cref="InstantNotificationOrderTracking"/> domain model to an <see cref="InstantNotificationOrderResponseExt"/> response model.
    /// </summary>
    /// <param name="source">The tracking information for the instant notification order.</param>
    /// <returns>
    /// An <see cref="InstantNotificationOrderResponseExt"/> containing the order chain identifier and notification shipment details.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="source"/> or its <c>Notification</c> property is <c>null</c>.
    /// </exception>
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
    /// Maps from an external instant notification order request to an instant notification order domain model.
    /// </summary>
    /// <param name="source">The external request model to map from.</param>
    /// <param name="creatorShortName">The short name of the creator of the notification.</param>
    /// <returns>An instant notification order domain model.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
    /// <exception cref="ArgumentException">Thrown when creatorShortName is null or empty.</exception>
    public static InstantNotificationOrder MapToInstantNotificationOrder(this InstantNotificationOrderRequestExt source, string creatorShortName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.InstantNotificationRecipient);
        ArgumentNullException.ThrowIfNull(source.InstantNotificationRecipient.ShortMessageDeliveryDetails);
        ArgumentNullException.ThrowIfNull(source.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent);

        ArgumentException.ThrowIfNullOrEmpty(creatorShortName);

        return new InstantNotificationOrder
        {
            OrderId = Guid.NewGuid(),
            Created = DateTime.UtcNow,
            OrderChainId = Guid.NewGuid(),
            IdempotencyId = source.IdempotencyId,
            Creator = new Creator(creatorShortName),
            SendersReference = source.SendersReference,
            InstantNotificationRecipient = MapToInstantNotificationRecipient(source.InstantNotificationRecipient)
        };
    }

    /// <summary>
    /// Maps from an external short message content model to a domain content model.
    /// </summary>
    /// <param name="source">The external short message content model to map from.</param>
    /// <returns>A domain model representing the short message content.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
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
    /// Maps from an external notification recipient model to a domain recipient model.
    /// </summary>
    /// <param name="source">The external recipient model to map from.</param>
    /// <returns>A domain model representing the notification recipient.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or its required properties are null.</exception>
    private static InstantNotificationRecipient MapToInstantNotificationRecipient(this InstantNotificationRecipientExt source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.ShortMessageDeliveryDetails);

        return new InstantNotificationRecipient
        {
            ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
            {
                PhoneNumber = source.ShortMessageDeliveryDetails.PhoneNumber,
                TimeToLiveInSeconds = source.ShortMessageDeliveryDetails.TimeToLiveInSeconds,
                ShortMessageContent = MapToShortMessageContent(source.ShortMessageDeliveryDetails.ShortMessageContent)
            }
        };
    }
}
