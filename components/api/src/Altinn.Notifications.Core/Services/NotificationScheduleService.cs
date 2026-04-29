using System.Runtime.InteropServices;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services
{
    /// <summary>
    /// Provides scheduling logic for SMS and email notifications.
    /// </summary>
    public class NotificationScheduleService : INotificationScheduleService
    {
        private readonly TimeSpan _smsSendWindowEndTime;
        private readonly TimeSpan _smsSendWindowStartTime;
        private readonly TimeSpan _emailSendWindowEndTime;
        private readonly TimeSpan _emailSendWindowStartTime;

        private readonly IDateTimeService _dateTimeService;

        private readonly TimeZoneInfo _norwegainTimeZoneInfo;
        private const string _norwegainTimeZoneIdLinux = "Europe/Oslo";
        private const string _norwegainTimeZoneIdWindows = "W. Europe Standard Time";

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationScheduleService"/> class.
        /// </summary>
        public NotificationScheduleService(
            IDateTimeService dateTimeService,
            IOptions<NotificationConfig> config)
        {
            _dateTimeService = dateTimeService;

            _smsSendWindowEndTime = new(config.Value.SmsSendWindowEndHour, 0, 0);
            _smsSendWindowStartTime = new(config.Value.SmsSendWindowStartHour, 0, 0);
            _emailSendWindowEndTime = new(config.Value.EmailSendWindowEndHour, 0, 0);
            _emailSendWindowStartTime = new(config.Value.EmailSendWindowStartHour, 0, 0);

            var norwegainTimeZoneId =
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? _norwegainTimeZoneIdWindows : _norwegainTimeZoneIdLinux;
            _norwegainTimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(norwegainTimeZoneId);
        }

        /// <inheritdoc/>
        public bool CanSendSmsNow()
        {
            return IsWithinWindow(_dateTimeService.UtcNow(), _smsSendWindowStartTime, _smsSendWindowEndTime);
        }

        /// <inheritdoc/>
        public bool CanSendEmailNow()
        {
            return IsWithinWindow(_dateTimeService.UtcNow(), _emailSendWindowStartTime, _emailSendWindowEndTime);
        }

        /// <inheritdoc/>
        public DateTime GetSmsExpirationDateTime(DateTime referenceUtcDateTime)
        {
            return GetExpirationDateTime(referenceUtcDateTime, _smsSendWindowStartTime, _smsSendWindowEndTime);
        }

        /// <inheritdoc/>
        public DateTime GetEmailExpirationDateTime(DateTime referenceUtcDateTime)
        {
            return GetExpirationDateTime(referenceUtcDateTime, _emailSendWindowStartTime, _emailSendWindowEndTime);
        }

        private bool IsWithinWindow(DateTime utcNow, TimeSpan windowStart, TimeSpan windowEnd)
        {
            var equivalentDateTimeInNorway = GetEquivalentDateTimeInNorway(utcNow);
            return equivalentDateTimeInNorway.TimeOfDay > windowStart && equivalentDateTimeInNorway.TimeOfDay < windowEnd;
        }

        private DateTime GetExpirationDateTime(DateTime referenceUtcDateTime, TimeSpan windowStart, TimeSpan windowEnd)
        {
            var equivalentDateTimeInNorway = GetEquivalentDateTimeInNorway(referenceUtcDateTime);

            if (equivalentDateTimeInNorway.TimeOfDay > windowStart && equivalentDateTimeInNorway.TimeOfDay < windowEnd)
            {
                return referenceUtcDateTime.AddHours(48);
            }

            double hoursToAdd = equivalentDateTimeInNorway.TimeOfDay < windowStart ? 48 : 72;

            DateTime baseDateTime = equivalentDateTimeInNorway.Date.Add(windowStart);

            DateTime expiryDateTime = baseDateTime.AddHours(hoursToAdd);

            return TimeZoneInfo.ConvertTimeToUtc(expiryDateTime, _norwegainTimeZoneInfo);
        }

        /// <summary>
        /// Converts a UTC <see cref="DateTime"/> to the equivalent time in the Norwegian time zone.
        /// </summary>
        /// <param name="dateTimeUTC">The UTC time to convert.</param>
        /// <returns>The equivalent time in the Norwegian time zone.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="dateTimeUTC"/> is not in UTC format.</exception>
        private DateTime GetEquivalentDateTimeInNorway(DateTime dateTimeUTC)
        {
            if (dateTimeUTC.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("DateTime must be in UTC format.", nameof(dateTimeUTC));
            }

            return TimeZoneInfo.ConvertTimeFromUtc(dateTimeUTC, _norwegainTimeZoneInfo);
        }
    }
}
