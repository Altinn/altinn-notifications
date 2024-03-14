namespace LinkMobility.PSWin.Receiver.Model
{
    /// <summary>
    /// Delivery Report message class
    /// </summary>
    public class DrMessage
    {
        /// <summary>
        /// Constructor for a delivery report message
        /// </summary>
        public DrMessage(string id, string reference, string receiver, DeliveryState state, string deliverytime)
        {
            Id = id;
            Reference = reference;
            Receiver = receiver;
            State = state;
            Deliverytime = deliverytime;
        }

        /// <summary>
        /// The id of the DR
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// The gateway reference of the DR
        /// </summary>
        public string Reference { get; }

        /// <summary>
        /// The receiver of the SMS
        /// </summary>
        public string Receiver { get; }

        /// <summary>
        /// The stats of the delivery
        /// </summary>
        public DeliveryState State { get; }

        /// <summary>
        /// Delivery time
        /// </summary>
        public string Deliverytime { get; }

        /// <summary>
        /// Boolean indicating wether the SMS was delivered or not
        /// </summary>
        public bool IsDelivered => State == DeliveryState.DELIVRD;
    }


    /// <summary>
    /// Enum describing all delivery states
    /// </summary>
    public enum DeliveryState
    {
        UNKNOWN,  // No information of delivery status available.
        DELIVRD,  // Message was successfully delivered to destination.
        EXPIRED,  // Message validity period has expired.
        DELETED,  // Message has been deleted.
        UNDELIV,  // The SMS was undeliverable (not a valid number or no available route to destination).
        REJECTD,  // Message was rejected.
        FAILED,  // The SMS failed to be delivered because no operator accepted the message or due to internal Gateway error.
        NULL,  // No delivery report received from operator. Unknown delivery status.

        // The following status codes will apply specially for Premium messages
        BARRED,  // The receiver number is barred/blocked/not in use. Do not retry message, and remove number from any subscriber list.
        BARREDA,  // The receiver could not receive the message because his/her age is below the specified AgeLimit.
        ZEROBAL,  // The receiver has an empty prepaid account.
    }
}
