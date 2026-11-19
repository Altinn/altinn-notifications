using Parquet.Serialization.Attributes;

namespace Altinn.Notifications.Core.Models.Metrics
{
    /// <summary>
    /// Record describing the notification metrics for a single email
    /// </summary>
    public record DailyEmailMetricsRecord
    {
        /// <summary>
        /// The unique identifier for the email.
        /// </summary>
        public long EmailId { get; init; }

        /// <summary>
        /// The shipment identifier of the email notification (email.alternateid), as UUID text.
        /// </summary>
        public string EmailShipmentId { get; init; } = string.Empty;

        /// <summary>
        /// The shipment identifier of the order the email belongs to (order.alternateid), as UUID text.
        /// </summary>
        public string OrderShipmentId { get; init; } = string.Empty;

        /// <summary>
        /// The sender's reference associated with the shipment.
        /// </summary>
        public string SendersReference { get; init; } = string.Empty;

        /// <summary>
        /// The requested send time for the email (UTC). Written as TIMESTAMP_MILLIS.
        /// </summary>
        [ParquetTimestamp(ParquetTimestampResolution.Milliseconds)]
        public DateTime RequestedSendTime { get; init; }

        /// <summary>
        /// The time the send result was set for the email (UTC). Written as TIMESTAMP_MILLIS.
        /// </summary>
        [ParquetTimestamp(ParquetTimestampResolution.Milliseconds)]
        public DateTime ResultTime { get; init; }

        /// <summary>
        /// The creator (originating service) name for the email.
        /// </summary>
        public string CreatorName { get; init; } = string.Empty;

        /// <summary>
        /// The resource identifier associated with the email, if any.
        /// </summary>
        public string? ResourceId { get; init; }

        /// <summary>
        /// The send result identifier or status for the email.
        /// </summary>
        public string Result { get; init; } = string.Empty;

        /// <summary>
        /// The operationId returned by the email provider.
        /// </summary>
        public string OperationId { get; init; } = string.Empty;
    }
}
