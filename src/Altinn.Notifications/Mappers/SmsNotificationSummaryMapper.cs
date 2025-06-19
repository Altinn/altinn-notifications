using Altinn.Notifications.Core.Models.Notification;

namespace Altinn.Notifications.Mappers
{
    /// <summary>
    /// Mapper for <see cref="SmsNotificationSummaryExt"/>
    /// </summary>
    public static class SmsNotificationSummaryMapper
    {
        /// <summary>
        /// Maps a <see cref="SmsNotificationSummary"/> to a <see cref="SmsNotificationSummaryExt"/>
        /// </summary>
        public static SmsNotificationSummaryExt MapToSmsNotificationSummaryExt(this SmsNotificationSummary summary)
        {
            return new SmsNotificationSummaryExt()
            {
                OrderId = summary.OrderId,
                SendersReference = summary.SendersReference,
                Generated = summary.Generated,
                Succeeded = summary.Succeeded,
                Notifications = summary.Notifications.MapToSmsNotificationWithResultExt(),
            };
        }

        /// <summary>
        /// Maps a list of <see cref="SmsNotificationWithResult"/> to a list of <see cref="SmsNotificationWithResultExt"/>
        /// </summary>
        public static List<SmsNotificationWithResultExt> MapToSmsNotificationWithResultExt(this List<SmsNotificationWithResult> notifications)
        {
            List<SmsNotificationWithResultExt> result = notifications.Select(n => n.MapToSmsNotificationWithResultExt()).ToList();

            return result;
        }

        /// <summary>
        /// Maps a <see cref="SmsNotificationWithResult"/> to a <see cref="SmsNotificationWithResultExt"/>
        /// </summary>       
        public static SmsNotificationWithResultExt MapToSmsNotificationWithResultExt(this SmsNotificationWithResult notification)
        {
            return new SmsNotificationWithResultExt()
            {
                Id = notification.Id,
                Succeeded = notification.Succeeded,
                Recipient = new()
                {
                    OrganizationNumber = notification.Recipient.OrganizationNumber,
                    NationalIdentityNumber = notification.Recipient.NationalIdentityNumber,
                    MobileNumber = notification.Recipient.MobileNumber
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
        private static string MapStatus(SmsNotificationWithResult notification)
        {
            if (notification.ResultStatus.Result == Core.Enums.SmsNotificationResultType.Failed_TTL)
            {
                return Core.Enums.SmsNotificationResultType.Failed.ToString();
            }

            return notification.ResultStatus.Result.ToString();
        }
    }
}
