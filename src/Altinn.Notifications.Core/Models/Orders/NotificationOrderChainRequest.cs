using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Represents the core business entity of a notification order request with reminders.
/// </summary>
public class NotificationOrderChainRequest
{
    /// <summary>
    /// Prevents a default instance of the <see cref="NotificationOrderChainRequest"/> class from being created.
    /// </summary>
    private NotificationOrderChainRequest()
    {
    }

    /// <summary>
    /// Gets a URI endpoint that can determine whether the notification should be sent.
    /// </summary>
    /// <remarks>
    /// When specified, the system will call this endpoint before sending the notification.
    /// The notification will only be sent if the endpoint returns a positive response.
    /// This enables conditional delivery based on external business rules or state.
    /// </remarks>
    public Uri? ConditionEndpoint { get; private set; }

    /// <summary>
    /// Gets the creator of the notification order sequence request.
    /// </summary>
    public Creator Creator { get; private set; } = new Creator(string.Empty);

    /// <summary>
    /// Gets the optional identifiers for one or more dialogs or transmissions in Dialogporten.
    /// </summary>
    /// <remarks>
    /// When specified, this associates the notification with specific dialogs or transmissions
    /// in the Dialogporten service, enabling integration between notifications and Dialogporten.
    /// </remarks>
    public DialogportenIdentifiers? DialogportenAssociation { get; private set; }

    /// <summary>
    /// Gets the idempotency identifier defined by the sender.
    /// </summary>
    public string IdempotencyId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the unique identifier for the main notification order in the sequence.
    /// </summary>
    /// <value>
    /// A <see cref="Guid"/> representing the unique identifier of the main notification order.
    /// </value>
    public Guid OrderId { get; private set; }

    /// <summary>
    /// Gets the unique identifier for the entire notification order chain.
    /// </summary>
    /// <value>
    /// A <see cref="Guid"/> representing the unique identifier of the notification order chain.
    /// </value>
    public Guid OrderChainId { get; private set; }

    /// <summary>
    /// Gets the recipient information for this notification.
    /// </summary>
    /// <remarks>
    /// Specifies the target recipient through one of the supported channels:
    /// email address, SMS number, national identity number, or organization number.
    /// The reminder can be directed to a different recipient than the initial notification.
    /// </remarks>
    public NotificationRecipient Recipient { get; private set; } = new NotificationRecipient();

    /// <summary>
    /// Gets a list of reminders that may be triggered under certain conditions after the initial notification has been processed.
    /// </summary>
    /// <remarks>
    /// Each reminder can have its own recipient settings, delay period, and triggering conditions.
    /// </remarks>
    public List<NotificationReminder>? Reminders { get; private set; }

    /// <summary>
    /// Gets the earliest date and time when the notification should be delivered.
    /// </summary>
    /// <remarks>
    /// Allows scheduling notifications for future delivery. The system will not deliver the notification
    /// before this time, but may deliver it later depending on system load and availability.
    /// Defaults to the current UTC time if not specified.
    /// </remarks>
    public DateTime RequestedSendTime { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the sender's reference identifier.
    /// </summary>
    /// <remarks>
    /// An optional identifier used to correlate the notification with records in the sender's system.
    /// </remarks>
    public string? SendersReference { get; private set; }

    /// <summary>
    /// Builder class for <see cref="NotificationOrderChainRequest"/>.
    /// </summary>
    public class NotificationOrderChainRequestBuilder
    {
        private Guid _orderId;
        private Guid _orderChainId;
        private Uri? _conditionEndpoint;
        private string? _sendersReference;
        private Creator _creator = new(string.Empty);
        private string _idempotencyId = string.Empty;
        private List<NotificationReminder>? _reminders;
        private NotificationRecipient _recipient = new();
        private DateTime _requestedSendTime = DateTime.UtcNow;
        private DialogportenIdentifiers? _dialogportenAssociation;

        /// <summary>
        /// Sets the order identifier for the notification request.
        /// </summary>
        public NotificationOrderChainRequestBuilder SetOrderId(Guid orderId)
        {
            _orderId = orderId;
            return this;
        }

        /// <summary>
        /// Sets the order chain identifier for the notification request.
        /// </summary>
        public NotificationOrderChainRequestBuilder SetOrderChainId(Guid orderChainId)
        {
            _orderChainId = orderChainId;
            return this;
        }

        /// <summary>
        /// Sets the creator of the notification request.
        /// </summary>
        public NotificationOrderChainRequestBuilder SetCreator(Creator creator)
        {
            _creator = creator ?? throw new ArgumentNullException(nameof(creator));
            return this;
        }

        /// <summary>
        /// Sets the idempotency identifier for the notification request.
        /// </summary>
        public NotificationOrderChainRequestBuilder SetIdempotencyId(string idempotencyId)
        {
            _idempotencyId = idempotencyId ?? throw new ArgumentNullException(nameof(idempotencyId));
            return this;
        }

        /// <summary>
        /// Sets the recipient information for the notification.
        /// </summary>
        public NotificationOrderChainRequestBuilder SetRecipient(NotificationRecipient recipient)
        {
            _recipient = recipient ?? throw new ArgumentNullException(nameof(recipient));
            return this;
        }

        /// <summary>
        /// Sets the condition endpoint for the notification.
        /// </summary>
        public NotificationOrderChainRequestBuilder SetConditionEndpoint(Uri? conditionEndpoint)
        {
            _conditionEndpoint = conditionEndpoint;
            return this;
        }

        /// <summary>
        /// Sets the Dialogporten association for the notification.
        /// </summary>
        public NotificationOrderChainRequestBuilder SetDialogportenAssociation(DialogportenIdentifiers? dialogportenAssociation)
        {
            _dialogportenAssociation = dialogportenAssociation;
            return this;
        }

        /// <summary>
        /// Sets the reminders for the notification.
        /// </summary>
        public NotificationOrderChainRequestBuilder SetReminders(List<NotificationReminder>? reminders)
        {
            _reminders = reminders;
            return this;
        }

        /// <summary>
        /// Sets the requested send time for the notification.
        /// </summary>
        public NotificationOrderChainRequestBuilder SetRequestedSendTime(DateTime? requestedSendTime)
        {
            _requestedSendTime = requestedSendTime ?? DateTime.UtcNow;
            return this;
        }

        /// <summary>
        /// Sets the sender's reference for the notification.
        /// </summary>
        public NotificationOrderChainRequestBuilder SetSendersReference(string? sendersReference)
        {
            _sendersReference = sendersReference;
            return this;
        }

        /// <summary>
        /// Builds and returns a new instance of NotificationOrderChainRequest.
        /// </summary>
        public NotificationOrderChainRequest Build()
        {
            if (_orderId == Guid.Empty)
            {
                throw new InvalidOperationException("OrderId must be set.");
            }

            if (_orderChainId == Guid.Empty)
            {
                throw new InvalidOperationException("OrderChainId must be set.");
            }

            if (string.IsNullOrEmpty(_idempotencyId))
            {
                throw new InvalidOperationException("IdempotencyId must be set.");
            }

            if (_creator == null || string.IsNullOrWhiteSpace(_creator.ShortName))
            {
                throw new InvalidOperationException("Creator name must be set.");
            }

            return new NotificationOrderChainRequest
            {
                OrderId = _orderId,
                Creator = _creator,
                Reminders = _reminders,
                Recipient = _recipient,
                OrderChainId = _orderChainId,
                IdempotencyId = _idempotencyId,
                SendersReference = _sendersReference,
                RequestedSendTime = _requestedSendTime,
                ConditionEndpoint = _conditionEndpoint,
                DialogportenAssociation = _dialogportenAssociation
            };
        }
    }
}
