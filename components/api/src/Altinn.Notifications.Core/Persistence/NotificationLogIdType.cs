namespace Altinn.Notifications.Core.Persistence
{
    /// <summary>
    /// Defines the type of ID used to retrieve notification log entries. This enum is used to specify whether the ID provided is a shipment ID, dialog ID, or transmission ID when querying the notification logs.
    /// </summary>
    public enum NotificationLogIdType
    {
        /// <summary>
        /// The ID is a shipment ID, which is the same as the notification order ID. This is the most common type and is used when retrieving logs for a specific notification order.
        /// </summary>
        ShipmentId,
        
        /// <summary>
        /// The ID is a dialog ID, which is used when retrieving logs for a specific dialog.
        /// </summary>
        DialogId,
        
        /// <summary>
        /// The ID is a transmission ID, which is used when retrieving logs for a specific transmission.
        /// </summary>
        TransmissionId
    }
}
