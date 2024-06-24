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
        private readonly IDateTimeService _dateTimeService;
        private readonly NotificationConfig _config;
        private readonly string _timeZoneId;
        private const string _norwayTimeZoneIdWindows = "W. Europe Standard Time";
        private const string _norwayTimeZoneIdLinux = "Europe/Oslo";

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationScheduleService"/> class.
        /// </summary>
        public NotificationScheduleService(IDateTimeService dateTimeService, IOptions<NotificationConfig> config)
        {
            _dateTimeService = dateTimeService;
            _config = config.Value;
            _timeZoneId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? _norwayTimeZoneIdWindows : _norwayTimeZoneIdLinux;
        }

        /// <inheritdoc/>
        public bool CanSendSmsNotifications()
        {
            DateTime dateTimeUtc = _dateTimeService.UtcNow();
            TimeZoneInfo norwayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(_timeZoneId);

            DateTime norwayTime = TimeZoneInfo.ConvertTimeFromUtc(dateTimeUtc, norwayTimeZone);

            TimeSpan startTime = new TimeSpan(_config.SmsSendWindowStartHour, 0, 0);
            TimeSpan endTime = new TimeSpan(_config.SmsSendWindowEndHour, 0, 0);

            TimeSpan currentTime = norwayTime.TimeOfDay;

            return startTime < currentTime && currentTime < endTime;
        }
    }
}
