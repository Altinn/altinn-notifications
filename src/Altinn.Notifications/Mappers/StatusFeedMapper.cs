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
        /// <param name="logger">The logger used for logging errors</param>
        /// <returns></returns>
        public static List<StatusFeedExt> MapToStatusFeedExtList<T>(this List<StatusFeed> orderStatuses, ILogger<T> logger)
        {
            if (orderStatuses == null || orderStatuses.Count == 0)
            {
                return [];
            }

            return [.. orderStatuses.Select(x => MapToStatusFeedExt(x, logger))];
        }

        private static StatusFeedExt MapToStatusFeedExt<T>(StatusFeed status, ILogger<T> logger)
        {
            return new StatusFeedExt
            {
                ShipmentId = status.OrderStatus.ShipmentId,
                LastUpdated = status.OrderStatus.LastUpdated,
                SendersReference = status.OrderStatus.SendersReference,
                SequenceNumber = status.SequenceNumber,
                Recipients = status.OrderStatus.Recipients.ToRecipientsExt(),
                Status = NotificationDeliveryManifestMapper.MapProcessingLifecycle(status.OrderStatus.Status)
            };
        }

        private static IImmutableList<RecipientExt> ToRecipientsExt(this IImmutableList<Recipient> recipients)
        {
            if (recipients == null || recipients.Count == 0)
            {
                return ImmutableList<RecipientExt>.Empty;
            }

            return [.. recipients.Select(r => new RecipientExt
            {
               Destination = r.Destination,
               Status = NotificationDeliveryManifestMapper.MapProcessingLifecycle(r.Status),
               LastUpdate = r.LastUpdate
            })];
        }
    }
}
