namespace Altinn.Notifications.Core.Models.Status
{
    /// <summary>
    /// Represents a status feed.
    /// </summary>
    public record OrderStatus
    {
        /// <summary>
        /// The sequence number of the status feed entry
        /// </summary>
        public int SequenceNumber { get; set; }

        /// <summary>
        /// The shipment id of the order
        /// </summary>
        public Guid ShipmentId { get; set; }

        /// <summary>
        /// The reference id used by the sender
        /// </summary>
        public string? SendersReference { get; set; }

        /// <summary>
        /// Status of the shipment
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// The status update time
        /// </summary>
        public DateTime LastUpdated { get; internal set; } = DateTime.UtcNow;

        /// <summary>
        /// Notification or reminder
        /// </summary>
        public string? ShipmentType { get; set; }

        /// <summary>
        /// The list of recipients of this shipment
        /// </summary>
        public List<object>? Recipients { get; set; }
    }
}
