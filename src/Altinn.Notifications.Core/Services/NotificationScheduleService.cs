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

            var (equivalentNorwayDateTime, sendWindowStartTime, sendWindowEndTime) = GetEquivalentNorwayTimeAndSendWindow(dateTimeUtc);

            TimeSpan equivalentNorwayTime = equivalentNorwayDateTime.TimeOfDay;

            return equivalentNorwayTime > sendWindowStartTime && equivalentNorwayTime < sendWindowEndTime;
        }

        /// <inheritdoc/>
        public DateTime GetSmsExpiryDateTime(DateTime referenceDateTime)
        {
            var (equivalentNorwayDateTime, sendWindowStartTime, sendWindowEndTime) = GetEquivalentNorwayTimeAndSendWindow(referenceDateTime);

            TimeSpan equivalentNorwayTime = equivalentNorwayDateTime.TimeOfDay;

            DateTime referenceExpiryDateTime;

            if (equivalentNorwayTime > sendWindowStartTime && equivalentNorwayTime < sendWindowEndTime)
            {
                referenceExpiryDateTime = equivalentNorwayDateTime;
            }
            else
            {
                var startTimeForToday = equivalentNorwayDateTime.Date.Add(sendWindowStartTime);
                referenceExpiryDateTime = equivalentNorwayTime < sendWindowStartTime ? startTimeForToday : startTimeForToday.AddDays(1);
            }

            return referenceExpiryDateTime.AddHours(48);
        }

        /// <summary>
        /// Retrieves the corresponding local time in Norway, along with the configured start and end times for sending SMS messages.
        /// </summary>
        /// <param name="dateTimeUtc">The UTC time to convert to Norway local time.</param>
        /// <returns>
        /// A tuple containing:
        /// <list type="table">
        ///   <item><description>The corresponding Norway local time for the given UTC input.</description></item>
        ///   <item><description>The start time of the SMS send window.</description></item>
        ///   <item><description>The end time of the SMS send window.</description></item>
        /// </list>
        /// </returns>
        private (DateTime EquivalentNorwayDateTime, TimeSpan SendWindowStartTime, TimeSpan SendWindowEndTime) GetEquivalentNorwayTimeAndSendWindow(DateTime dateTimeUtc)
        {
            DateTime equivalentNorwayTime;

            switch (dateTimeUtc.Kind)
            {
                case DateTimeKind.Utc:
                    TimeZoneInfo norwayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(_timeZoneId);
                    equivalentNorwayTime = TimeZoneInfo.ConvertTimeFromUtc(dateTimeUtc, norwayTimeZone);
                    break;

                default:
                    throw new ArgumentException("DateTime must be in UTC format.", nameof(dateTimeUtc));
            }

            TimeSpan sendWindowStartTime = new(_config.SmsSendWindowStartHour, 0, 0);
            TimeSpan sendWindowEndTime = new(_config.SmsSendWindowEndHour, 0, 0);

            return (equivalentNorwayTime, sendWindowStartTime, sendWindowEndTime);
        }
    }
}
