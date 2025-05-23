using System.Text.Json;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Models.Delivery;

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
            try
            {
                using JsonDocument jsonDocument = JsonDocument.Parse(status.OrderStatus);
                var jsonElement = jsonDocument.RootElement.Clone();

                return new StatusFeedExt
                {
                    SequenceNumber = status.SequenceNumber,
                    OrderStatus = jsonElement
                };
            }
            catch (Exception e)
            {
                // Log or handle the exception as needed
                logger.LogError(e, "Failed to parse OrderStatus JSON for SequenceNumber {SequenceNumber}", status.SequenceNumber);

                using JsonDocument jsonDocument = JsonDocument.Parse("{}");
                var emptyJsonElement = jsonDocument.RootElement.Clone();

                return new StatusFeedExt
                {
                    SequenceNumber = status.SequenceNumber,
                    OrderStatus = emptyJsonElement
                };
            }
        }
    }
}
