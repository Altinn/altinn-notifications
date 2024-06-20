using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services
{
    /// <summary>
    /// Implementation of the <see cref="INotificationScheduleService"/> using the "W. Europe Standard Time".
    /// </summary>
    public class NotificationScheduleService : INotificationScheduleService
    {
        private readonly IDateTimeService _dateTimeService;
        private readonly NotificationOrderConfig _config;
        private const string _norwayTimeZoneId = "W. Europe Standard Time";

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationScheduleService"/> class.
        /// </summary>
        public NotificationScheduleService(IDateTimeService dateTimeService, IOptions<NotificationOrderConfig> config)
        {
            _dateTimeService = dateTimeService;
            _config = config.Value;
        }

        /// <inheritdoc/>
        public bool CanSendSmsNotifications()
        {
            DateTime dateTimeUtc = _dateTimeService.UtcNow();
            TimeZoneInfo norwayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(_norwayTimeZoneId);

            DateTime norwayTime = TimeZoneInfo.ConvertTimeFromUtc(dateTimeUtc, norwayTimeZone);

            TimeSpan startTime = new TimeSpan(_config.SmsSendWindowStartHour, 0, 0);
            TimeSpan endTime = new TimeSpan(_config.SmsSendWindowEndHour, 0, 0);

            TimeSpan currentTime = norwayTime.TimeOfDay;

            return startTime < currentTime && currentTime < endTime;
        }
    }
}
