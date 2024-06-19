using Altinn.Notifications.Configuration;

namespace Altinn.Notifications.Extensions
{
    /// <summary>
    /// An extension class for all date time related actions
    /// </summary>
    public static class DateTimeExtensions
    {
        private const string NorwayTimeZoneId = "W. Europe Standard Time";

        /// <summary>
        /// Checks if provided DateTime is outside the business hours in local time zone.
        /// </summary>
        /// <param name="dateTime">The date time object to check.</param>
        /// <returns>A boolean indicating whether it is within business hours or not.</returns>
        /// <exception cref="InvalidOperationException">Thrown is any issues arise with time zone processing.</exception>
        public static bool IsWithinBusinessHours(this DateTime dateTime)
        {
            try
            {
                DateTime dateTimeUtc = dateTime.Kind switch
                {
                    DateTimeKind.Local => dateTime.ToUniversalTime(),
                    DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
                    _ => dateTime
                };

                TimeZoneInfo norwayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(NorwayTimeZoneId);

                DateTime norwayTime = TimeZoneInfo.ConvertTimeFromUtc(dateTimeUtc, norwayTimeZone);

                TimeSpan startTime = new TimeSpan(GeneralSettings.SmsSendWindowStartHour, 0, 0);
                TimeSpan endTime = new TimeSpan(GeneralSettings.SmsSendWindowEndHour, 0, 0);

                TimeSpan currentTime = norwayTime.TimeOfDay;

                return startTime < currentTime && currentTime < endTime;
            }
            catch (TimeZoneNotFoundException)
            {
                throw new InvalidOperationException($"The time zone ID '{NorwayTimeZoneId}' was not found on the local system.");
            }
            catch (InvalidTimeZoneException)
            {
                throw new InvalidOperationException($"The time zone ID '{NorwayTimeZoneId}' is invalid.");
            }
        }
    }
}
