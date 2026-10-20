using Parquet.Serialization.Attributes;

namespace Altinn.Notifications.Core.Models.Metrics
{
    /// <summary>
    /// Record describing the notification metrics for a single sms
    /// </summary>
    public record DailySmsMetricsRecord
    {
        /// <summary>
        /// Gets or sets the unique identifier for the SMS.
        /// </summary>
        public long SmsId { get; init; }

        /// <summary>
        /// Gets or sets the shipment identifier of the SMS notification (sms.alternateid), as UUID text.
        /// </summary>
        public string SmsShipmentId { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the shipment identifier of the order the SMS belongs to (order.alternateid), as UUID text.
        /// </summary>
        public string OrderShipmentId { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the sender's reference associated with the shipment.
        /// </summary>
        public string SendersReference { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the requested send time for the SMS (UTC). Written as TIMESTAMP_MILLIS.
        /// </summary>
        [ParquetTimestamp(ParquetTimestampResolution.Milliseconds)]
        public DateTime RequestedSendTime { get; init; }

        /// <summary>
        /// Gets or sets the time the send result was set for the SMS (UTC). Written as TIMESTAMP_MILLIS.
        /// </summary>
        [ParquetTimestamp(ParquetTimestampResolution.Milliseconds)]
        public DateTime ResultTime { get; init; }

        /// <summary>
        /// Gets or sets the creator (originating service) name for the SMS.
        /// </summary>
        public string CreatorName { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the resource identifier associated with the SMS, if any.
        /// </summary>
        public string? ResourceId { get; init; }

        /// <summary>
        /// Gets or sets the send result identifier or status for the SMS.
        /// </summary>
        public string Result { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the gateway reference returned by the SMS provider.
        /// </summary>
        public string GatewayReference { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the mobile number prefix (country code) used for the SMS recipient.
        /// </summary>
        public string MobileNumberPrefix { get; init; } = string.Empty;
    }
}
