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
                    OrganisationNumber = notification.Recipient.OrganisationNumber,
                    NationalIdentityNumber = notification.Recipient.NationalIdentityNumber,
                    MobileNumber = notification.Recipient.MobileNumber
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
