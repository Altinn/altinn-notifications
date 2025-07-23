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
        private const string _norwayTimeZoneIdLinux = "Europe/Oslo";
        private readonly Mock<IDateTimeService> _dateTimeMock = new();
        private const string _norwayTimeZoneIdWindows = "W. Europe Standard Time";
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
        public void CanSendSmsNow_WhenCurrentTimeIsWithinSendWindow_ReturnsTrue()
        {
            // Arrange
            var currentDateTime = new DateTime(2022, 1, 1, 10, 0, 0, DateTimeKind.Utc); // 10:00 UTC is 11:00 or 12:00 local time
            _dateTimeMock.Setup(e => e.UtcNow()).Returns(currentDateTime);

            // Act
            var result = _notificationScheduleService.CanSendSmsNow();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanSendSmsNow_WhenCurrentTimeIsAfterSendWindow_ReturnsFalse()
        {
            // Arrange
            var currentDateTime = new DateTime(2022, 1, 1, 20, 0, 0, DateTimeKind.Utc); // 20:00 UTC is 21:00 or 22:00 local time
            _dateTimeMock.Setup(e => e.UtcNow()).Returns(currentDateTime);

            // Act
            var result = _notificationScheduleService.CanSendSmsNow();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanSendSmsNow_WhenCurrentTimeIsBeforeSendWindow_ReturnsFalse()
        {
            // Arrange
            var currentDateTime = new DateTime(2022, 1, 1, 5, 0, 0, DateTimeKind.Utc); // 05:00 UTC is 07:00 or 08:00 local time
            _dateTimeMock.Setup(e => e.UtcNow()).Returns(currentDateTime);

            // Act
            var result = _notificationScheduleService.CanSendSmsNow();

            // Assert
            Assert.False(result);
        }


        [Fact]
        public void GetSmsExpiryDateTime_RequestSendTimeIsWithinSendWindow_ReturnsNextStartTime()
        {
            // Arrange
            var requestedSendTime = new DateTime(2025, 08, 25, 10, 0, 0, DateTimeKind.Utc); // 10:00 UTC is 11:00 or 12:00 local time

            var expectedExpiryDateTime = new DateTime(2025, 08, 27, 10, 0, 0, DateTimeKind.Utc);

            // Act
            var expiryDateTime = _notificationScheduleService.GetSmsExpirationDateTime(requestedSendTime);

            // Assert
            Assert.Equal(expectedExpiryDateTime, expiryDateTime);
        }

        [Fact]
        public void GetSmsExpiryDateTime_WhenReferenceTimeIsAfterSendWindow_ReturnsNextSendWindowStartDateTime()
        {
            // Arrange
            var requestedSendTime = new DateTime(2025, 08, 25, 20, 0, 0, DateTimeKind.Utc); // 20:00 UTC is 21:00 or 22:00 local time

            var expectedExpiryDateTimeInLocalFormat = new DateTime(2025, 08, 28, 09, 0, 0, DateTimeKind.Unspecified);

            var expectedExpiryDateTimeInUTCFormat = ConvertNorwegianLocalTimeToUtc(expectedExpiryDateTimeInLocalFormat);

            // Act
            var expiryDateTime = _notificationScheduleService.GetSmsExpirationDateTime(requestedSendTime);

            // Assert
            Assert.Equal(expectedExpiryDateTimeInUTCFormat, expiryDateTime);
        }

        [Fact]
        public void GetSmsExpiryDateTime_WhenReferenceTimeIsBeforeSendWindow_ReturnsNextSendWindowStartDateTime()
        {
            // Arrange
            var requestedSendTime = new DateTime(2025, 08, 25, 05, 0, 0, DateTimeKind.Utc); // 05:00 UTC is 06:00 or 07:00 local time

            var expectedExpiryDateTimeInLocalFormat = new DateTime(2025, 08, 27, 09, 0, 0, DateTimeKind.Unspecified);

            var expectedExpiryDateTimeInUTCFormat = ConvertNorwegianLocalTimeToUtc(expectedExpiryDateTimeInLocalFormat);

            // Act
            var expiryDateTime = _notificationScheduleService.GetSmsExpirationDateTime(requestedSendTime);

            // Assert
            Assert.Equal(expectedExpiryDateTimeInUTCFormat, expiryDateTime);
        }

        [Theory]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public void GetSmsExpirationDateTime_WhenReferenceDateTimeIsNotUtc_ThrowsArgumentException(DateTimeKind kind)
        {
            // Arrange
            var nonUtcDateTime = new DateTime(2025, 8, 25, 10, 0, 0, kind);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _notificationScheduleService.GetSmsExpirationDateTime(nonUtcDateTime));
        }

        /// <summary>
        /// Converts a local Norwegian time (Europe/Oslo or W. Europe Standard Time) to its corresponding UTC time.
        /// This is used in tests to calculate the expected UTC expiry time from a local time value.
        /// </summary>
        /// <param name="localNorwegianTime">
        /// The local time in Norway (DateTimeKind.Unspecified) to convert to UTC.
        /// </param>
        /// <returns>
        /// The equivalent UTC <see cref="DateTime"/>.
        /// </returns>
        private static DateTime ConvertNorwegianLocalTimeToUtc(DateTime localNorwegianTime)
        {
            var timeZoneId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? _norwayTimeZoneIdWindows : _norwayTimeZoneIdLinux;

            TimeZoneInfo norwayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

            return TimeZoneInfo.ConvertTimeToUtc(localNorwegianTime, norwayTimeZone);
        }
    }
}
