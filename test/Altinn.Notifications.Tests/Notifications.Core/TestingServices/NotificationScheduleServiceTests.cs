using System;
using System.Runtime.InteropServices;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices
{
    public class NotificationScheduleServiceTests
    {
        private readonly Mock<IDateTimeService> _dateTimeMock = new();
        private readonly NotificationScheduleService _notificationScheduleService;

        public NotificationScheduleServiceTests()
        {
            NotificationConfig config = new()
            {
                SmsSendWindowStartHour = 9,
                SmsSendWindowEndHour = 17
            };

            _notificationScheduleService = new(_dateTimeMock.Object, Options.Create(config));
        }

        [Fact]
        public void IsWithinSmsSendWindow_WhenCurrentTimeIsWithinSmsSendWindow_ReturnsTrue()
        {
            // Arrange
            var dateTime = new DateTime(2022, 1, 1, 10, 0, 0, DateTimeKind.Utc); // 10:00 UTC is 11:00 or 12:00 local time
            _dateTimeMock.Setup(e => e.UtcNow()).Returns(dateTime);

            // Act
            var result = _notificationScheduleService.IsWithinSmsSendWindow();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsWithinSmsSendWindow_WhenCurrentTimeIsAfterSmsSendWindow_ReturnsFalse()
        {
            // Arrange
            var dateTime = new DateTime(2022, 1, 1, 20, 0, 0, DateTimeKind.Utc); // 20:00 UTC is 21:00 or 22:00 local time
            _dateTimeMock.Setup(e => e.UtcNow()).Returns(dateTime);

            // Act
            var result = _notificationScheduleService.IsWithinSmsSendWindow();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsWithinSmsSendWindow_WhenCurrentTimeIsBeforeSmsSendWindow_ReturnsFalse()
        {
            // Arrange
            var dateTime = new DateTime(2022, 1, 1, 5, 0, 0, DateTimeKind.Utc); // 05:00 UTC is 07:00 or 08:00 local time
            _dateTimeMock.Setup(e => e.UtcNow()).Returns(dateTime);

            // Act
            var result = _notificationScheduleService.IsWithinSmsSendWindow();

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(2022, 1, 1, 10, 0, 0)] // 10:00 local time, within window
        [InlineData(2022, 1, 1, 12, 0, 0)] // 12:00 local time, within window
        public void GetSmsExpiryDateTime_WithinSendWindow_ReturnsNowPlus48Hours(int year, int month, int day, int hour, int minute, int second)
        {
            // Arrange
            var dateTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
            _dateTimeMock.Setup(e => e.UtcNow()).Returns(dateTime);

            // Act
            var expiry = _notificationScheduleService.GetSmsExpiryDateTime();

            // Assert
            Assert.Equal(dateTime.AddHours(48), expiry);
        }

        [Theory]
        [InlineData(2022, 1, 1, 7, 0, 0)] // 07:00 local time, before window
        [InlineData(2022, 1, 1, 8, 59, 59)] // 08:59:59 local time, before window
        public void GetSmsExpiryDateTime_BeforeSendWindow_ReturnsStartTimeTodayPlus48Hours(int year, int month, int day, int hour, int minute, int second)
        {
            // Arrange
            var dateTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
            _dateTimeMock.Setup(e => e.UtcNow()).Returns(dateTime);

            var startTimeToday = new DateTime(year, month, day, 9, 0, 0, DateTimeKind.Utc);

            // Act
            var expiry = _notificationScheduleService.GetSmsExpiryDateTime();

            // Assert
            Assert.Equal(startTimeToday.AddHours(48), expiry);
        }

        [Fact]
        public void GetSmsExpiryDateTime_AfterSendWindow_ReturnsNextStartTimePlus48Hours()
        {
            // Arrange
            var dateTime = new DateTime(2022, 1, 1, 17, 0, 0, DateTimeKind.Utc); // 17:00 UTC is 19:00 or 20:00 local time
            _dateTimeMock.Setup(e => e.UtcNow()).Returns(dateTime);

            // Set the next start time to 9:00 local time on the next day
            var nextStartLocal = new DateTime(2022, 1, 2, 9, 0, 0, DateTimeKind.Unspecified);
            var utcTime = TimeZoneInfo.ConvertTimeToUtc(nextStartLocal, TimeZoneInfo.FindSystemTimeZoneById(GetNorwayTimeZoneId()));
            _dateTimeMock.Setup(e => e.UtcNow()).Returns(utcTime);

            // Act
            var expiry = _notificationScheduleService.GetSmsExpiryDateTime();

            // Assert
            Assert.Equal(nextStartLocal.AddHours(48), expiry);
        }

        /// <summary>
        /// Gets the norway time zone identifier.
        /// </summary>
        /// <returns></returns>
        private static string GetNorwayTimeZoneId()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "W. Europe Standard Time" : "Europe/Oslo";
        }
    }
}
