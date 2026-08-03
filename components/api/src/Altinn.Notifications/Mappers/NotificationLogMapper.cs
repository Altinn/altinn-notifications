using System.Collections.Immutable;

using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Models.NotificationLog;

namespace Altinn.Notifications.Mappers;

/// <summary>
/// Provides mapping functionality between <see cref="NotificationLogEntry"/> domain models
/// and their corresponding <see cref="NotificationLogEntryExt"/> external representations.
/// </summary>
public static class NotificationLogMapper
{
    /// <summary>
    /// Maps a collection of <see cref="NotificationLogEntry"/> domain models to their external representations.
    /// </summary>
    /// <param name="entries">The collection of domain models to map.</param>
    /// <returns>An immutable list of <see cref="NotificationLogEntryExt"/> instances.</returns>
    public static IImmutableList<NotificationLogEntryExt> MapToNotificationLogEntryExtList(this IImmutableList<NotificationLogEntry> entries)
    {
        return [.. entries.Select(MapToNotificationLogEntryExt)];
    }

    /// <summary>
    /// Maps a single <see cref="NotificationLogEntry"/> domain model to its external representation.
    /// </summary>
    /// <param name="entry">The domain model to map.</param>
    /// <returns>A <see cref="NotificationLogEntryExt"/> instance.</returns>
    private static NotificationLogEntryExt MapToNotificationLogEntryExt(this NotificationLogEntry entry)
    {
        return new NotificationLogEntryExt
        {
            OrderChainId = entry.OrderChainId,
            ShipmentId = entry.ShipmentId,
            NotificationId = entry.NotificationId,
            CreatorName = entry.CreatorName,
            SendersReference = entry.SendersReference,
            DialogId = entry.DialogId,
            TransmissionId = entry.TransmissionId,
            DeliveryReference = entry.DeliveryReference,
            Recipient = entry.Recipient,
            Type = entry.Type,
            Channel = entry.Channel,
            Destination = entry.Destination,
            Resource = entry.Resource,
            Status = entry.Status,
            RequestedSendTime = entry.RequestedSendTime,
            LastUpdateTime = entry.LastUpdateTime
        };
    }
}
