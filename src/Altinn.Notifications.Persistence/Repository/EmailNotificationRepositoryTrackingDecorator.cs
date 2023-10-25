using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Repository.Interfaces;

using Microsoft.ApplicationInsights;

namespace Altinn.Notifications.Persistence.Repository
{
    /// <summary>
    /// Decorator class introducing telemetry tracking for the <see cref="IEmailNotificationRepository"/>.
    /// </summary>
    public class EmailNotificationRepositoryTrackingDecorator : IEmailNotificationRepository
    {
        private readonly IEmailNotificationRepository _decoratedRepository;
        private readonly TelemetryClient? _telemetryClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailNotificationRepositoryTrackingDecorator"/> class.
        /// </summary>
        public EmailNotificationRepositoryTrackingDecorator(
            IEmailNotificationRepository decoratedRepository,
            TelemetryClient? telemetryClient = null)
        {
            _decoratedRepository = decoratedRepository;
            _telemetryClient = telemetryClient;
        }

        /// <inheritdoc/>
        public async Task AddNotification(EmailNotification notification, DateTime expiry)
        {
            using TelemetryTracker tracker = new(_telemetryClient);
            await _decoratedRepository.AddNotification(notification, expiry);

            tracker.Track(EmailNotificationRepository._insertEmailNotificationSql);
        }

        /// <inheritdoc/>
        public async Task<List<Email>> GetNewNotifications()
        {
            using TelemetryTracker tracker = new(_telemetryClient);
            List<Email> searchResult = await _decoratedRepository.GetNewNotifications();

            tracker.Track(EmailNotificationRepository._getEmailNotificationsSql);
            return searchResult;
        }

        /// <inheritdoc/>
        public async Task<List<EmailRecipient>> GetRecipients(Guid notificationId)
        {
            using TelemetryTracker tracker = new(_telemetryClient);
            List<EmailRecipient> searchResult = await _decoratedRepository.GetRecipients(notificationId);

            tracker.Track(EmailNotificationRepository._getEmailRecipients);
            return searchResult;
        }

        /// <inheritdoc/>
        public async Task UpdateSendStatus(Guid notificationId, EmailNotificationResultType status, string? operationId = null)
        {
            using TelemetryTracker tracker = new(_telemetryClient);
            await _decoratedRepository.UpdateSendStatus(notificationId, status, operationId);
            tracker.Track(EmailNotificationRepository._updateEmailStatus);
        }
    }
}
