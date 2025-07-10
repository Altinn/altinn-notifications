using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
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
    /// Maps an external instant notification order request to a core domain model.
    /// </summary>
    /// <param name="source">The external request model to map from.</param>
    /// <param name="creatorShortName">The short name of the creator of the notification.</param>
    /// <returns>A domain model representing the instant notification order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
    /// <exception cref="ArgumentException">Thrown when creatorShortName is null or empty.</exception>
    public static InstantNotificationOrder ToInstantNotificationOrder(this InstantNotificationOrderRequestExt source, string creatorShortName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(creatorShortName);

        return new InstantNotificationOrder
        {
            OrderId = Guid.NewGuid(),
            Created = DateTime.UtcNow,
            OrderChainId = Guid.NewGuid(),
            IdempotencyId = source.IdempotencyId,
            Creator = new Creator(creatorShortName),
            SendersReference = source.SendersReference,
            Recipient = MapToInstantNotificationRecipient(source.InstantNotificationRecipient)
        };
    }

    /// <summary>
    /// Maps an external short message content model to a core domain content model.
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
    /// Maps an external notification recipient model to a core domain recipient model.
    /// </summary>
    /// <param name="source">The external recipient model to map from.</param>
    /// <returns>A domain model representing the notification recipient.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
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
