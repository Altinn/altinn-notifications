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
        /// <summary>
        /// A daily send window in Norwegian local time, from <paramref name="Start"/> to <paramref name="End"/> (both exclusive).
        /// </summary>
        private readonly record struct SendWindow(TimeSpan Start, TimeSpan End);

        private readonly SendWindow _smsSendWindow;
        private readonly SendWindow _emailSendWindow;

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

            _smsSendWindow = new(new(config.Value.SmsSendWindowStartHour, 0, 0), new(config.Value.SmsSendWindowEndHour, 0, 0));
            _emailSendWindow = new(new(config.Value.EmailSendWindowStartHour, 0, 0), new(config.Value.EmailSendWindowEndHour, 0, 0));

            var norwegainTimeZoneId =
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? _norwegainTimeZoneIdWindows : _norwegainTimeZoneIdLinux;
            _norwegainTimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(norwegainTimeZoneId);
        }

        /// <inheritdoc/>
        public bool CanSendSmsNow()
        {
            return IsWithinSendWindow(_smsSendWindow);
        }

        /// <inheritdoc/>
        public DateTime GetSmsExpirationDateTime(DateTime referenceUtcDateTime)
        {
            return GetExpirationDateTime(referenceUtcDateTime, _smsSendWindow);
        }

        /// <inheritdoc/>
        public bool CanSendEmailNow()
        {
            return IsWithinSendWindow(_emailSendWindow);
        }

        /// <inheritdoc/>
        public DateTime GetEmailExpirationDateTime(DateTime referenceUtcDateTime)
        {
            return GetExpirationDateTime(referenceUtcDateTime, _emailSendWindow);
        }

        /// <summary>
        /// Determines whether the current time, in the Norwegian time zone, is within the given send window.
        /// </summary>
        private bool IsWithinSendWindow(SendWindow sendWindow)
        {
            DateTime dateTimeUtc = _dateTimeService.UtcNow();

            var equivalentDateTimeInNorway = GetEquivalentDateTimeInNorway(dateTimeUtc);

            return equivalentDateTimeInNorway.TimeOfDay > sendWindow.Start && equivalentDateTimeInNorway.TimeOfDay < sendWindow.End;
        }

        /// <summary>
        /// Calculates the expiry for a notification restricted to the given send window.
        /// </summary>
        private DateTime GetExpirationDateTime(DateTime referenceUtcDateTime, SendWindow sendWindow)
        {
            var equivalentDateTimeInNorway = GetEquivalentDateTimeInNorway(referenceUtcDateTime);

            if (equivalentDateTimeInNorway.TimeOfDay > sendWindow.Start && equivalentDateTimeInNorway.TimeOfDay < sendWindow.End)
            {
                return referenceUtcDateTime.AddHours(48);
            }

            double hoursToAdd = equivalentDateTimeInNorway.TimeOfDay < sendWindow.Start ? 48 : 72;

            DateTime baseDateTime = equivalentDateTimeInNorway.Date.Add(sendWindow.Start);

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
