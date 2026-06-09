using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;

namespace Altinn.Notifications.Mappers
{
    /// <summary>
    /// Mapper for <see cref="EmailNotificationSummaryExt"/>
    /// </summary>
    public static class EmailNotificationSummaryMapper
    {
        /// <summary>
        /// Maps a <see cref="EmailNotificationSummary"/> to a <see cref="EmailNotificationSummaryExt"/>
        /// </summary>
        public static EmailNotificationSummaryExt MapToEmailNotificationSummaryExt(this EmailNotificationSummary summary)
        {
            return new EmailNotificationSummaryExt()
            {
                OrderId = summary.OrderId,
                SendersReference = summary.SendersReference,
                Generated = summary.Generated,
                Succeeded = summary.Succeeded,
                Notifications = summary.Notifications.MapToEmailNotificationWithResultExt(),
            };
        }

        /// <summary>
        /// Maps a list of <see cref="EmailNotificationWithResult"/> to a list of <see cref="EmailNotificationWithResultExt"/>
        /// </summary>
        public static List<EmailNotificationWithResultExt> MapToEmailNotificationWithResultExt(this List<EmailNotificationWithResult> notifications)
        {
            List<EmailNotificationWithResultExt> result = notifications.Select(n => n.MapToEmailNotificationWithResultExt()).ToList();

            return result;
        }

        /// <summary>
        /// Maps a  <see cref="EmailNotificationWithResult"/> to a <see cref="EmailNotificationWithResultExt"/>
        /// </summary>       
        public static EmailNotificationWithResultExt MapToEmailNotificationWithResultExt(this EmailNotificationWithResult notification)
        {
            return new EmailNotificationWithResultExt()
            {
                Id = notification.Id,
                Succeeded = notification.Succeeded,
                Recipient = new()
                {
                    OrganizationNumber = notification.Recipient.OrganizationNumber,
                    NationalIdentityNumber = notification.Recipient.NationalIdentityNumber,
                    EmailAddress = notification.Recipient.ToAddress
                },
                SendStatus = new()
                {
                    Status = MapStatus(notification),
                    StatusDescription = notification.ResultStatus.ResultDescription,
                    LastUpdate = notification.ResultStatus.ResultTime
                }
            };
        }

        /// <summary>
        /// This will map future FailedTTL status to Failed for v1 API.
        /// </summary>
        /// <param name="notification">The notification to be mapped</param>
        /// <returns></returns>
        private static string MapStatus(EmailNotificationWithResult notification)
        {
            if (notification.ResultStatus.Result == EmailNotificationResultType.Failed_TTL)
            {
                return EmailNotificationResultType.Failed.ToString();
            }

            return notification.ResultStatus.Result.ToString();
        }
    }
}
