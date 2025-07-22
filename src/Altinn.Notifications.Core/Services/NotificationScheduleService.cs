using System.Runtime.InteropServices;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services
{
    /// <summary>
    /// Provides scheduling logic for SMS notifications.
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

            var (localDateTime, sendWindowStartTime, sendWindowEndTime) = GetLocalDateTimeAndSendWindow(dateTimeUtc);

            return localDateTime.TimeOfDay > sendWindowStartTime && localDateTime.TimeOfDay < sendWindowEndTime;
        }

        /// <inheritdoc/>
        public DateTime GetSmsExpiryDateTime(DateTime referenceDateTime)
        {
            var (localDateTime, sendWindowStartTime, sendWindowEndTime) = GetLocalDateTimeAndSendWindow(referenceDateTime);

            if (localDateTime.TimeOfDay > sendWindowStartTime && localDateTime.TimeOfDay < sendWindowEndTime)
            {
                return referenceDateTime.AddHours(48);
            }

            var nextSendWindowStartDateTime = localDateTime.Date.Add(sendWindowStartTime);

            TimeZoneInfo norwgianTimeZone = TimeZoneInfo.FindSystemTimeZoneById(_timeZoneId);

            var expiryDateTimeInLocalZone = localDateTime.TimeOfDay < sendWindowStartTime ? nextSendWindowStartDateTime : nextSendWindowStartDateTime.AddDays(1);

            DateTime expiryUtc = TimeZoneInfo.ConvertTimeToUtc(expiryDateTimeInLocalZone, norwgianTimeZone);

            var expiryDateTime = new DateTime(expiryUtc.Year, expiryUtc.Month, expiryUtc.Day + 2, expiryUtc.Hour, expiryUtc.Minute, expiryUtc.Second, DateTimeKind.Utc);

            return expiryDateTime;
        }

        /// <summary>
        /// Retrieves the corresponding local time in Norwegian time zone, along with the configured start and end times for sending SMS messages.
        /// </summary>
        /// <param name="referenceUtcDateTime">The UTC time to convert to local time.</param>
        /// <returns>
        /// A tuple containing:
        /// <list type="table">
        ///   <item><description>The local time in Norwegian time zone.</description></item>
        ///   <item><description>The start time of the SMS send window.</description></item>
        ///   <item><description>The end time of the SMS send window.</description></item>
        /// </list>
        /// </returns>
        /// <exception cref="System.ArgumentException">DateTime must be in UTC format.</exception>
        private (DateTime LocalDateTime, TimeSpan SendWindowStartTime, TimeSpan SendWindowEndTime) GetLocalDateTimeAndSendWindow(DateTime referenceUtcDateTime)
        {
            DateTime localDateTime;

            switch (referenceUtcDateTime.Kind)
            {
                case DateTimeKind.Utc:
                    TimeZoneInfo norwayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(_timeZoneId);
                    localDateTime = TimeZoneInfo.ConvertTimeFromUtc(referenceUtcDateTime, norwayTimeZone);
                    break;

                default:
                    throw new ArgumentException("DateTime must be in UTC format.");
            }

            TimeSpan sendWindowStartTime = new(_config.SmsSendWindowStartHour, 0, 0);
            TimeSpan sendWindowEndTime = new(_config.SmsSendWindowEndHour, 0, 0);

            return (localDateTime, sendWindowStartTime, sendWindowEndTime);
        }
    }
}
