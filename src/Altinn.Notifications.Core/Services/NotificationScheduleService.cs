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
        private readonly TimeSpan _sendWindowEndTime;
        private readonly TimeSpan _sendWindowStartTime;

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

            _sendWindowEndTime = new(config.Value.SmsSendWindowEndHour, 0, 0);
            _sendWindowStartTime = new(config.Value.SmsSendWindowStartHour, 0, 0);

            var norwegainTimeZoneId =
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? _norwegainTimeZoneIdWindows : _norwegainTimeZoneIdLinux;
            _norwegainTimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(norwegainTimeZoneId);
        }

        /// <inheritdoc/>
        public bool CanSendSmsNow()
        {
            DateTime dateTimeUtc = _dateTimeService.UtcNow();

            var equivalentDateTimeInNorway = GetEquivalentDateTimeInNorway(dateTimeUtc);

            return equivalentDateTimeInNorway.TimeOfDay > _sendWindowStartTime && equivalentDateTimeInNorway.TimeOfDay < _sendWindowEndTime;
        }

        /// <inheritdoc/>
        public DateTime GetSmsExpirationDateTime(DateTime referenceUtcDateTime)
        {
            var equivalentDateTimeInNorway = GetEquivalentDateTimeInNorway(referenceUtcDateTime);

            if (equivalentDateTimeInNorway.TimeOfDay > _sendWindowStartTime && equivalentDateTimeInNorway.TimeOfDay < _sendWindowEndTime)
            {
                return referenceUtcDateTime.AddHours(48);
            }

            DateTime baseDateTime = equivalentDateTimeInNorway.Date.Add(_sendWindowStartTime);

            double hoursToAdd = equivalentDateTimeInNorway.TimeOfDay < _sendWindowStartTime ? 48 : 72;

            DateTime expiryDateTime = baseDateTime.AddHours(hoursToAdd);

            return TimeZoneInfo.ConvertTimeToUtc(expiryDateTime, _norwegainTimeZoneInfo);
        }

        /// <summary>
        /// Converts a UTC <see cref="DateTime"/> to the equivalent time in the Norwegian time zone.
        /// </summary>
        /// <param name="dateTimeUTC">The UTC time to convert.</param>
        /// <returns>The equivalent time in the Norwegian time zone.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="dateTimeUTC"/> is not in UTC.</exception>
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
