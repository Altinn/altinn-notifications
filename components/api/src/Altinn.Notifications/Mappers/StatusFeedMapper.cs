using System.Collections.Immutable;
using Altinn.Notifications.Core.Models.Status;
using Altinn.Notifications.Models.Status;

namespace Altinn.Notifications.Mappers
{
    /// <summary>
    /// Provides mapping functionality between domain models and their corresponding external data transfer models.
    /// </summary>
    public static class StatusFeedMapper
    {
        /// <summary>
        /// Maps a list of domain order statuses to their external representation for API responses.
        /// </summary>
        /// <param name="orderStatuses">List of order status domain model objects</param>
        /// <returns></returns>
        public static List<StatusFeedExt> MapToStatusFeedExtList(this List<StatusFeed> orderStatuses)
        {
            if (orderStatuses == null || orderStatuses.Count == 0)
            {
                return [];
            }

            return [.. orderStatuses.Select(MapToStatusFeedExt)];
        }

        private static StatusFeedExt MapToStatusFeedExt(StatusFeed status)
        {
            return new StatusFeedExt
            {
                ShipmentId = status.OrderStatus.ShipmentId,
                LastUpdated = status.OrderStatus.LastUpdated,
                ShipmentType = status.OrderStatus.ShipmentType,
                SendersReference = status.OrderStatus.SendersReference,
                SequenceNumber = status.SequenceNumber,
                Recipients = status.OrderStatus.Recipients.ToRecipientsExt(),
                Status = NotificationDeliveryManifestMapper.MapProcessingLifecycle(status.OrderStatus.Status)
            };
        }

        private static ImmutableList<StatusFeedRecipientExt> ToRecipientsExt(this IImmutableList<Recipient> recipients)
        {
            if (recipients == null || recipients.Count == 0)
            {
                return ImmutableList<StatusFeedRecipientExt>.Empty;
            }

            return [.. recipients.Select(r => new StatusFeedRecipientExt
            {
               Destination = r.Destination,
               Status = NotificationDeliveryManifestMapper.MapProcessingLifecycle(r.Status),
               LastUpdate = r.LastUpdate
            })];
        }
    }
}
