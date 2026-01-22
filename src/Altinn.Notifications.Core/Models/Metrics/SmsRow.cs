namespace Altinn.Notifications.Core.Models.Metrics
{
    /// <summary>
    /// Record describing the notification metrics for a single sms
    /// </summary>
    public record SmsRow
    {
        /// <summary>
        /// Gets or sets the unique identifier for the SMS.
        /// </summary>
        public long SmsId { get; init; }

        /// <summary>
        /// Gets or sets the shipment identifier this SMS belongs to.
        /// </summary>
        public string ShipmentId { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the sender's reference associated with the shipment.
        /// </summary>
        public string SendersReference { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the requested send time for the SMS.
        /// </summary>
        public string RequestedSendtime { get; init; } = string.Empty;

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
        /// Gets or sets the rate category or tariff used for the SMS.
        /// </summary>
        public string Rate { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the mobile number prefix (country code) used for the SMS recipient.
        /// </summary>
        public string MobileNumberPrefix { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of Altinn SMS messages used to send the message (segments).
        /// </summary>
        public int AltinnSmsCount { get; init; }

        /// <summary>
        /// Gets or sets the length of the Altinn SMS body in characters, if available.
        /// </summary>
        public int AltinnSmsBodyLength { get; init; }

        /// <summary>
        /// Gets or sets the length of any customized SMS body in characters.
        /// </summary>
        public int AltinnSmsCustomBodyLength { get; init; }
    }
}
