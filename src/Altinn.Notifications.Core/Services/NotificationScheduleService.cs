using System.Runtime.InteropServices;

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
        private readonly string _timeZoneId;
        private readonly NotificationConfig _config;
        private readonly IDateTimeService _dateTimeService;
        private const string _norwayTimeZoneIdLinux = "Europe/Oslo";
        private const string _norwayTimeZoneIdWindows = "W. Europe Standard Time";

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationScheduleService"/> class.
        /// </summary>
        public NotificationScheduleService(
            IDateTimeService dateTimeService,
            IOptions<NotificationConfig> config)
        {
            _config = config.Value;
            _dateTimeService = dateTimeService;
            _timeZoneId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? _norwayTimeZoneIdWindows : _norwayTimeZoneIdLinux;
        }

        /// <inheritdoc/>
        public bool IsWithinSmsSendWindow()
        {
            DateTime dateTimeUtc = _dateTimeService.UtcNow();

            TimeZoneInfo norwayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(_timeZoneId);

            DateTime norwayTime = TimeZoneInfo.ConvertTimeFromUtc(dateTimeUtc, norwayTimeZone);

            TimeSpan startTime = new(_config.SmsSendWindowStartHour, 0, 0);
            TimeSpan endTime = new(_config.SmsSendWindowEndHour, 0, 0);

            TimeSpan currentTime = norwayTime.TimeOfDay;

            return startTime < currentTime && currentTime < endTime;
        }

        /// <inheritdoc/>
        public DateTime GetSmsExpiryDateTime()
        {
            DateTime dateTimeUtc = _dateTimeService.UtcNow();

            TimeZoneInfo norwayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(_timeZoneId);

            DateTime norwayTime = TimeZoneInfo.ConvertTimeFromUtc(dateTimeUtc, norwayTimeZone);

            TimeSpan startTime = new(_config.SmsSendWindowStartHour, 0, 0);
            TimeSpan endTime = new(_config.SmsSendWindowEndHour, 0, 0);

            TimeSpan currentTime = norwayTime.TimeOfDay;

            if (startTime < currentTime && currentTime < endTime)
            {
                // If the current time is within the allowed send window for SMS, set the expiry to 48 hours from now.
                return norwayTime.AddHours(48);
            }
            else if (currentTime < startTime)
            {
                // If the current time is before the allowed send window for SMS, set the expiry to 48 hours after today's window starts.
                return norwayTime.Date.Add(startTime).AddHours(48);
            }
            else
            {
                // If the current time is after the allowed send window for SMS, set the expiry to 48 hours after the next window starts.
                DateTime nextStart = norwayTime.Date.AddDays(1).Add(startTime);
                TimeSpan untilNextStart = nextStart - norwayTime;
                return norwayTime.Add(untilNextStart).AddHours(48);
            }
        }
    }
}
