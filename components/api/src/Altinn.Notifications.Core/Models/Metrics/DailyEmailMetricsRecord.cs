namespace Altinn.Notifications.Core.Models.Metrics
{
    /// <summary>
    /// Record describing the notification metrics for a single email
    /// </summary>
    public record DailyEmailMetricsRecord
    {
        /// <summary>
        /// Gets or sets the unique identifier for the email.
        /// </summary>
        public long EmailId { get; init; }

        /// <summary>
        /// Gets or sets the shipment identifier this email belongs to.
        /// </summary>
        public string ShipmentId { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the sender's reference associated with the shipment.
        /// </summary>
        public string SendersReference { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the requested send time for the email.
        /// </summary>
        public string RequestedSendTime { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the creator (originating service) name for the email.
        /// </summary>
        public string CreatorName { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the resource identifier associated with the email, if any.
        /// </summary>
        public string? ResourceId { get; init; }

        /// <summary>
        /// Gets or sets the send result identifier or status for the email.
        /// </summary>
        public string Result { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the operationId returned by the email provider.
        /// </summary>
        public string OperationId { get; init; } = string.Empty;
    }
}
