using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services
{
    /// <summary>
    /// Implementation of <see cref="IEmailNotificationSummaryService"/>
    /// </summary>
    public class EmailNotificationSummaryService : IEmailNotificationSummaryService
    {
        private readonly INotificationSummaryRepository _summaryRepository;
        private readonly static Dictionary<EmailNotificationResultType, string> _emailResultDescriptions = new()
            {
                { EmailNotificationResultType.New, "The email has been created, but has not been picked up for processing yet." },
                { EmailNotificationResultType.Sending, "The email is being processed and will be attempted sent shortly." },
                { EmailNotificationResultType.Succeeded, "The email has been accepted by the third party email service and will be sent shortly." },
                { EmailNotificationResultType.Delivered, "The email was delivered to the recipient. No errors reported, making it likely it was received by the recipient." },
                { EmailNotificationResultType.Failed, "The email was not sent due to an unspecified failure." },
                { EmailNotificationResultType.Failed_RecipientNotIdentified, "The email was not sent because the recipient's email address was not found." },
                { EmailNotificationResultType.Failed_InvalidEmailFormat, "The email was not sent because the recipient’s email address is in an invalid format." },
                { EmailNotificationResultType.Failed_SupressedRecipient, "The email was not sent because the recipient’s email address is suppressed by the third party email service." },
                { EmailNotificationResultType.Failed_TransientError, "The email was not sent due to a transient error. We will retry sending the email." },
                { EmailNotificationResultType.Failed_Bounced, "The email hard bounced, which may have happened because the email address does not exist or the domain is invalid." },
                { EmailNotificationResultType.Failed_FilteredSpam, "The email was was identified as spam, and was rejected or blocked (not quarantined)." },
                { EmailNotificationResultType.Failed_Quarantined, "The email was quarantined (as spam, bulk mail, or phising)." }
            };

        private readonly static List<EmailNotificationResultType> _successResults = new()
        {
            EmailNotificationResultType.Succeeded,
            EmailNotificationResultType.Delivered
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailNotificationSummaryService"/> class.
        /// </summary>
        public EmailNotificationSummaryService(INotificationSummaryRepository summaryRepository)
        {
            _summaryRepository = summaryRepository;
        }

        /// <inheritdoc/>
        public async Task<Result<EmailNotificationSummary, ServiceError>> GetSummary(Guid orderId, string creator)
        {
            EmailNotificationSummary? summary = await _summaryRepository.GetEmailSummary(orderId, creator);

            if (summary == null)
            {
                return new ServiceError(404);
            }

            if (summary.Notifications.Count != 0)
            {
                ProcessNotificationResults(summary);
            }

            return summary;
        }

        /// <summary>
        /// Processes the notification results setting counts and descriptions
        /// </summary>
        internal static void ProcessNotificationResults(EmailNotificationSummary summary)
        {
            summary.Generated = summary.Notifications.Count;

            foreach (EmailNotificationWithResult notification in summary.Notifications)
            {
                NotificationResult<EmailNotificationResultType> resultStatus = notification.ResultStatus;
                if (IsSuccessResult(resultStatus.Result))
                {
                    notification.Succeeded = true;
                    ++summary.Succeeded;
                }

                resultStatus.SetResultDescription(GetResultDescription(resultStatus.Result));
            }
        }

        /// <summary>
        /// Checks if the <see cref="EmailNotificationResultType"/> is a success result
        /// </summary>
        internal static bool IsSuccessResult(EmailNotificationResultType result)
        {
            return _successResults.Contains(result);
        }

        /// <summary>
        /// Gets the English description of the <see cref="EmailNotificationResultType"/>"
        /// </summary>
        internal static string GetResultDescription(EmailNotificationResultType result)
        {
            return _emailResultDescriptions[result];
        }
    }
}
