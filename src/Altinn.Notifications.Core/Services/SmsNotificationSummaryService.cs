using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services
{
    /// <summary>
    /// Implementation of <see cref="ISmsNotificationSummaryService"/>
    /// </summary>
    public class SmsNotificationSummaryService : ISmsNotificationSummaryService
    {
        private readonly INotificationSummaryRepository _summaryRepository;
        private readonly static Dictionary<SmsNotificationResultType, string> _smsResultDescriptions = new()
        {
            { SmsNotificationResultType.New, "The sms has been created, but has not been picked up for processing yet." },
            { SmsNotificationResultType.Sending, "The sms is being processed and will be attempted sent shortly." },
            { SmsNotificationResultType.Accepted, "The sms has been accepted by the gateway service." },
            { SmsNotificationResultType.Failed, "The sms was not sent due to an unspecified failure." },
            { SmsNotificationResultType.Failed_RecipientNotIdentified, "The sms was not sent because the recipient's sms address was not found." },
            { SmsNotificationResultType.Failed_InvalidRecipient, "The sms was not sent because the recipient number was invalid." }
        };

        private readonly static List<SmsNotificationResultType> _successResults = new()
        {
            SmsNotificationResultType.Accepted
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="SmsNotificationSummaryService"/> class.
        /// </summary>
        public SmsNotificationSummaryService(INotificationSummaryRepository summaryRepository)
        {
            _summaryRepository = summaryRepository;
        }

        /// <inheritdoc/>
        public async Task<Result<SmsNotificationSummary, ServiceError>> GetSummary(Guid orderId, string creator)
        {
            SmsNotificationSummary? summary = await _summaryRepository.GetSmsSummary(orderId, creator);

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
        internal static void ProcessNotificationResults(SmsNotificationSummary summary)
        {
            summary.Generated = summary.Notifications.Count;

            foreach (SmsNotificationWithResult notification in summary.Notifications)
            {
                NotificationResult<SmsNotificationResultType> resultStatus = notification.ResultStatus;
                if (IsSuccessResult(resultStatus.Result))
                {
                    notification.Succeeded = true;
                    ++summary.Succeeded;
                }

                resultStatus.SetResultDescription(GetResultDescription(resultStatus.Result));
            }
        }

        /// <summary>
        /// Checks if the <see cref="SmsNotificationResultType"/> is a success result
        /// </summary>
        internal static bool IsSuccessResult(SmsNotificationResultType result)
        {
            return _successResults.Contains(result);
        }

        /// <summary>
        /// Gets the English description of the <see cref="SmsNotificationResultType"/>"
        /// </summary>
        internal static string GetResultDescription(SmsNotificationResultType result)
        {
            return _smsResultDescriptions[result];
        }
    }
}
