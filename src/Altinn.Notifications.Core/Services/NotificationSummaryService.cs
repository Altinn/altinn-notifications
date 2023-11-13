using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services
{
    /// <summary>
    /// Implementation of <see cref="INotificationSummaryService"/>
    /// </summary>
    public class NotificationSummaryService : INotificationSummaryService
    {
        private readonly INotificationSummaryRepository _summaryRepository;
        private readonly Dictionary<EmailNotificationResultType, string> _emailResultDescriptions;

        private readonly List<EmailNotificationResultType> _successResults = new()
        {
            EmailNotificationResultType.Succeeded,
            EmailNotificationResultType.Delivered
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationSummaryService"/> class.
        /// </summary>
        public NotificationSummaryService(INotificationSummaryRepository summaryRepository)
        {
            _summaryRepository = summaryRepository;

            _emailResultDescriptions = new()
            {
                { EmailNotificationResultType.New, "The email has been created, but has not been picked up for processing yet." },
                { EmailNotificationResultType.Sending, "The email is being processed and will be attempted sent shortly." },
                { EmailNotificationResultType.Succeeded, "The email has been accepted by the third party email service and will be sent shortly." },
                { EmailNotificationResultType.Delivered, "The email was delivered to the recipient. No errors reported, making it likely it was received by the recipient." },
                { EmailNotificationResultType.Failed_RecipientNotIdentified, "Email was not sent because the recipient's email address was not found." }
            };
        }

        /// <inheritdoc/>
        public async Task<(EmailNotificationSummary? Summary, ServiceError? Error)> GetEmailSummary(Guid orderId, string creator)
        {
            EmailNotificationSummary? summary = await _summaryRepository.GetEmailSummary(orderId, creator);

            if (summary == null)
            {
                return (null, new ServiceError(404));
            }

            if (summary.Notifications.Any())
            {
                ProcessNotificationResults(summary);
            }

            return (summary, null);
        }

        private void ProcessNotificationResults(EmailNotificationSummary summary)
        {
            summary.Generated = summary.Notifications.Count;

            foreach (EmailNotificationWithResult notification in summary.Notifications)
            {
                NotificationResult<EmailNotificationResultType> resultStatus = notification.ResultStatus;
                if (_successResults.Contains(resultStatus.Result))
                {
                    notification.Succeeded = true;
                    ++summary.Succeeded;
                }

                resultStatus.SetResultDescription(_emailResultDescriptions[resultStatus.Result]);
            }
        }
    }
}
