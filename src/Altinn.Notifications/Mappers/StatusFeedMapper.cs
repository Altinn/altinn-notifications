using System.Collections.Immutable;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Models.Delivery;
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
        public static List<StatusFeedExt> MapToOrderStatusExtList(this List<StatusFeed> orderStatuses)
        {
            return orderStatuses.Select(MapToOrderStatusExt).ToList();
        }

        private static StatusFeedExt MapToOrderStatusExt(StatusFeed status)
        {
            return new StatusFeedExt
            {
                SequenceNumber = status.SequenceNumber,
                OrderStatus = status.OrderStatus
            };
        }
    }
}
