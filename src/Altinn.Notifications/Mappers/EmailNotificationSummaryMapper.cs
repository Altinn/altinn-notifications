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
                    EmailAddress = notification.Recipient.ToAddress
                },
                SendStatus = new()
                {
                    Status = notification.ResultStatus.Result.ToString(),
                    StatusDescription = notification.ResultStatus.ResultDescription,
                    LastUpdate = notification.ResultStatus.ResultTime
                }
            };
        }
    }
}
