using System.Collections.Immutable;

using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Models.NotificationLog;

namespace Altinn.Notifications.Mappers;

/// <summary>
/// Provides mapping functionality between <see cref="NotificationLogSummary"/> domain models
/// and their corresponding <see cref="NotificationLogSummaryExt"/> external representations.
/// </summary>
public static class NotificationLogMapper
{
    /// <summary>
    /// Maps a collection of <see cref="NotificationLogSummary"/> domain models to their external representations.
    /// </summary>
    /// <param name="entries">The collection of domain models to map.</param>
    /// <returns>An immutable list of <see cref="NotificationLogSummaryExt"/> instances.</returns>
    public static IImmutableList<NotificationLogSummaryExt> MapToNotificationLogSummaryList(this IImmutableList<NotificationLogSummary> entries)
    {
        return [.. entries.Select(MapToNotificationLogSummary)];
    }

    /// <summary>
    /// Maps a single <see cref="NotificationLogSummary"/> domain model to its external representation.
    /// </summary>
    /// <param name="entry">The domain model to map.</param>
    /// <returns>A <see cref="NotificationLogSummaryExt"/> instance.</returns>
    private static NotificationLogSummaryExt MapToNotificationLogSummary(this NotificationLogSummary entry)
    {
        return new NotificationLogSummaryExt
        {
            Type = entry.Type,
            Status = entry.Status,
            Channel = entry.Channel,
            DialogId = entry.DialogId,
            Destination = entry.Destination,
            TransmissionId = entry.TransmissionId,
            LastUpdateTime = entry.LastUpdateTime,
            NotificationId = entry.NotificationId,
            RequestedSendTime = entry.RequestedSendTime
        };
    }
}
