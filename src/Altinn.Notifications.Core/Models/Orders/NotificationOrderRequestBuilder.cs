using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;

namespace Altinn.Notifications.Core.Models.Orders
{
    /// <summary>
    /// Builder for the <see cref="NotificationOrderRequest"/> object
    /// </summary>
    public class NotificationOrderRequestBuilder
    {
        private string? _sendersReference;
        private bool _sendersReferenceSet;

        private List<INotificationTemplate>? _templates;
        private bool _templatesSet;

        private DateTime _requestedSendTime;
        private bool _requestedSendTimeSet;

        private NotificationChannel _notificationChannel;
        private bool _notificationChannelSet;

        private List<Recipient>? _recipients;
        private bool _recipientsSet;

        private Creator? _creator;
        private bool _creatorSet;

        private bool _ignoreReservation;
        private bool _ignoreReservationSet;

        /// <summary>
        /// Sets the senders reference
        /// </summary>
        public NotificationOrderRequestBuilder SetSendersReference(string? value)
        {
            _sendersReference = value;
            _sendersReferenceSet = true;
            return this;
        }

        /// <summary>
        /// Sets the notification templates
        /// </summary>
        public NotificationOrderRequestBuilder SetTemplates(List<INotificationTemplate> value)
        {
            _templates = value;
            _templatesSet = true;
            return this;
        }

        /// <summary>
        /// Sets the requested send time
        /// </summary>
        public NotificationOrderRequestBuilder SetRequestedSendTime(DateTime value)
        {
            _requestedSendTime = value;
            _requestedSendTimeSet = true;
            return this;
        }

        /// <summary>
        /// Sets the notification channel
        /// </summary>
        public NotificationOrderRequestBuilder SetNotificationChannel(NotificationChannel value)
        {
            _notificationChannel = value;
            _notificationChannelSet = true;
            return this;
        }

        /// <summary>
        /// Sets the recipients
        /// </summary>
        public NotificationOrderRequestBuilder SetRecipients(List<Recipient> value)
        {
            _recipients = value;
            _recipientsSet = true;
            return this;
        }

        /// <summary>
        /// Sets the creator
        /// </summary>
        public NotificationOrderRequestBuilder SetCreator(string value)
        {
            _creator = new(value);
            _creatorSet = true;
            return this;
        }

        /// <summary>
        /// Sets the ignore reservations
        /// </summary>
        public NotificationOrderRequestBuilder SetIgnoreReservation(bool value)
        {
            _ignoreReservation = value;
            _ignoreReservationSet = true;
            return this;
        }

        /// <summary>
        /// Constructs a new <see cref="NotificationOrderRequest"/> object
        /// </summary>
        public NotificationOrderRequest Build()
        {
            if (!_sendersReferenceSet ||
                !_templatesSet ||
                !_requestedSendTimeSet ||
                !_notificationChannelSet ||
                !_recipientsSet ||
                !_creatorSet ||
                !_ignoreReservationSet)
            {
                throw new ArgumentException("Not all required properties are set.");
            }

            return new NotificationOrderRequest()
            {
                SendersReference = _sendersReference,
                Creator = _creator!,
                Templates = _templates!,
                RequestedSendTime = _requestedSendTime,
                NotificationChannel = _notificationChannel,
                Recipients = _recipients!,
                IgnoreReservation = _ignoreReservation,
            };
        }
    }
}
