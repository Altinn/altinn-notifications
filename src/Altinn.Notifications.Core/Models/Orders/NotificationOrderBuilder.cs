using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;

namespace Altinn.Notifications.Core.Models.Orders
{
    /// <summary>
    /// Partial class implementation containing the builder logic
    /// </summary>
    public partial class NotificationOrder
    {
        /// <summary>
        /// Builder for the <see cref="NotificationOrder"/> object
        /// </summary>
        public class NotificationOrderBuilder
        {
            /// <summary>
            /// Boolean indicating whether the id has been set.
            /// </summary> 
            internal bool IdSet { get; private set; }

            /// <summary>
            /// Boolean indicating whether the requested send time has been set.
            /// </summary>
            internal bool RequestedSendTimeSet { get; private set; }

            /// <summary>
            /// Boolean indicating whether the notification channel has been set.
            /// </summary>
            internal bool NotificationChannelSet { get; private set; }

            /// <summary>
            /// Boolean indicating whether the creator has been set.
            /// </summary>
            internal bool CreatorSet { get; private set; }

            /// <summary>
            /// Boolean indicating whether the created date has been set.
            /// </summary>
            internal bool CreatedSet { get; private set; }

            /// <summary>
            /// Boolean indicating whether the templates have been set.
            /// </summary>
            internal bool TemplatesSet { get; private set; }

            /// <summary>
            /// Boolean indicating whether the recipients have been set.
            /// </summary>
            internal bool RecipientsSet { get; private set; }

            private Guid _id;
            private string? _sendersReference;
            private DateTime _requestedSendTime;
            private NotificationChannel _notificationChannel;
            private bool _ignoreReservation;
            private Creator? _creator;
            private DateTime _created;
            private List<INotificationTemplate>? _templates;
            private List<Recipient>? _recipients;

            /// <summary>
            /// Sets the Id of the NotificationOrder.
            /// </summary>
            public NotificationOrderBuilder SetId(Guid id)
            {
                _id = id;
                IdSet = true;
                return this;
            }

            /// <summary>
            /// Sets the SendersReference of the NotificationOrder.
            /// </summary>
            public NotificationOrderBuilder SetSendersReference(string? sendersReference)
            {
                _sendersReference = sendersReference;
                return this;
            }

            /// <summary>
            /// Sets the RequestedSendTime of the NotificationOrder.
            /// </summary>
            public NotificationOrderBuilder SetRequestedSendTime(DateTime requestedSendTime)
            {
                _requestedSendTime = requestedSendTime;
                RequestedSendTimeSet = true;
                return this;
            }

            /// <summary>
            /// Sets the NotificationChannel of the NotificationOrder.
            /// </summary>
            public NotificationOrderBuilder SetNotificationChannel(NotificationChannel notificationChannel)
            {
                _notificationChannel = notificationChannel;
                NotificationChannelSet = true;
                return this;
            }

            /// <summary>
            /// Sets the IgnoreReservation of the NotificationOrder.
            /// </summary>
            public NotificationOrderBuilder SetIgnoreReservation(bool ignoreReservation)
            {
                _ignoreReservation = ignoreReservation;
                return this;
            }

            /// <summary>
            /// Sets the Creator of the NotificationOrder.
            /// </summary>
            public NotificationOrderBuilder SetCreator(Creator creator)
            {
                _creator = creator;
                CreatorSet = true;
                return this;
            }

            /// <summary>
            /// Sets the Created date of the NotificationOrder.
            /// </summary>
            public NotificationOrderBuilder SetCreated(DateTime created)
            {
                _created = created;
                CreatedSet = true;
                return this;
            }

            /// <summary>
            /// Sets the Templates of the NotificationOrder.
            /// </summary>
            public NotificationOrderBuilder SetTemplates(List<INotificationTemplate> templates)
            {
                _templates = templates;
                TemplatesSet = true;
                return this;
            }

            /// <summary>
            /// Sets the Recipients of the NotificationOrder.
            /// </summary>
            public NotificationOrderBuilder SetRecipients(List<Recipient> recipients)
            {
                _recipients = recipients;
                RecipientsSet = true;
                return this;
            }

            /// <summary>
            /// Constructs a new <see cref="NotificationOrder"/> object
            /// </summary>
            public NotificationOrder Build()
            {
                if (!IdSet ||
                    !RequestedSendTimeSet ||
                    !NotificationChannelSet ||
                    !CreatorSet ||
                    !CreatedSet ||
                    !TemplatesSet ||
                    !RecipientsSet)
                {
                    throw new ArgumentException("Not all required properties are set for the notification order.");
                }

                var order = new NotificationOrder()
                {
                    Id = _id,
                    SendersReference = _sendersReference,
                    RequestedSendTime = _requestedSendTime,
                    NotificationChannel = _notificationChannel,
                    IgnoreReservation = _ignoreReservation,
                    Creator = _creator!,
                    Created = _created,
                    Templates = _templates!,
                    Recipients = _recipients!
                };

                return order;
            }

        }
    }
}
