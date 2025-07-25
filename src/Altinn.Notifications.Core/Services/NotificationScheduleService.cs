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
        public bool CanSendSmsNow()
        {
            DateTime dateTimeUtc = _dateTimeService.UtcNow();

            var (localEquivalentDateTime, sendWindowStartTime, sendWindowEndTime) = GetlocalEquivalentDateTimeAndSendWindow(dateTimeUtc);

            return localEquivalentDateTime.TimeOfDay > sendWindowStartTime && localEquivalentDateTime.TimeOfDay < sendWindowEndTime;
        }

        /// <inheritdoc/>
        public DateTime GetSmsExpirationDateTime(DateTime referenceUtcDateTime)
        {
            var (localEquivalentDateTime, sendWindowStartTime, sendWindowEndTime) = GetlocalEquivalentDateTimeAndSendWindow(referenceUtcDateTime);

            if (localEquivalentDateTime.TimeOfDay > sendWindowStartTime && localEquivalentDateTime.TimeOfDay < sendWindowEndTime)
            {
                return referenceUtcDateTime.AddHours(48);
            }

            DateTime expiryDateTime;

            if (localEquivalentDateTime.TimeOfDay < sendWindowStartTime)
            {
                expiryDateTime = localEquivalentDateTime.Date.Add(sendWindowStartTime).AddHours(48);
            }
            else
            {
                expiryDateTime = localEquivalentDateTime.Date.AddDays(1).Add(sendWindowStartTime).AddHours(48);
            }

            return expiryDateTime.ToUniversalTime();
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
        /// <exception cref="ArgumentException">DateTime must be in UTC format.</exception>
        private (DateTime LocalEquivalentDateTime, TimeSpan SendWindowStartTime, TimeSpan SendWindowEndTime) GetlocalEquivalentDateTimeAndSendWindow(DateTime referenceUtcDateTime)
        {
            if (referenceUtcDateTime.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("DateTime must be in UTC format.");
            }

            TimeZoneInfo norwayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(_timeZoneId);

            var localEquivalentDateTime = TimeZoneInfo.ConvertTimeFromUtc(referenceUtcDateTime, norwayTimeZone);

            TimeSpan sendWindowEndTime = new(_config.SmsSendWindowEndHour, 0, 0);

            TimeSpan sendWindowStartTime = new(_config.SmsSendWindowStartHour, 0, 0);

            return (localEquivalentDateTime, sendWindowStartTime, sendWindowEndTime);
        }
    }
}
