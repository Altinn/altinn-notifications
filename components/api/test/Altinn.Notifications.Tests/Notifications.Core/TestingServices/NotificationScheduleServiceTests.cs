using System;

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
                SmsSendWindowEndHour = 17,
                EmailSendWindowStartHour = 10,
                EmailSendWindowEndHour = 15
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

            var expectedExpiryDateTime = new DateTime(2025, 08, 28, 07, 0, 0, DateTimeKind.Utc);

            // Act
            var expiryDateTime = _notificationScheduleService.GetSmsExpirationDateTime(requestedSendTime);

            // Assert
            Assert.Equal(expectedExpiryDateTime, expiryDateTime);
        }

        [Fact]
        public void GetSmsExpiryDateTime_WhenReferenceTimeIsBeforeSendWindow_ReturnsNextSendWindowStartDateTime()
        {
            // Arrange
            var requestedSendTime = new DateTime(2025, 08, 25, 05, 0, 0, DateTimeKind.Utc); // 05:00 UTC is 06:00 or 07:00 local time

            var expectedExpiryDateTime = new DateTime(2025, 08, 27, 07, 0, 0, DateTimeKind.Utc);

            // Act
            var expiryDateTime = _notificationScheduleService.GetSmsExpirationDateTime(requestedSendTime);

            // Assert
            Assert.Equal(expectedExpiryDateTime, expiryDateTime);
        }

        [Theory]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public void GetSmsExpirationDateTime_WhenReferenceDateTimeIsNotUtc_ThrowsArgumentException(DateTimeKind kind)
        {
            // Arrange
            var nonUtcDateTime = new DateTime(2025, 8, 25, 10, 0, 0, kind);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _notificationScheduleService.GetSmsExpirationDateTime(nonUtcDateTime));
        }

        [Fact]
        public void CanSendEmailNow_WhenCurrentTimeIsWithinSendWindow_ReturnsTrue()
        {
            // Arrange
            var currentDateTime = new DateTime(2022, 1, 1, 10, 0, 0, DateTimeKind.Utc); // 10:00 UTC is 11:00 local time (window 10-15)
            _dateTimeMock.Setup(e => e.UtcNow()).Returns(currentDateTime);

            // Act
            var result = _notificationScheduleService.CanSendEmailNow();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanSendEmailNow_WhenCurrentTimeIsAfterSendWindow_ReturnsFalse()
        {
            // Arrange
            var currentDateTime = new DateTime(2022, 1, 1, 20, 0, 0, DateTimeKind.Utc); // 20:00 UTC is 21:00 local time
            _dateTimeMock.Setup(e => e.UtcNow()).Returns(currentDateTime);

            // Act
            var result = _notificationScheduleService.CanSendEmailNow();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanSendEmailNow_WhenCurrentTimeIsBeforeSendWindow_ReturnsFalse()
        {
            // Arrange
            var currentDateTime = new DateTime(2022, 1, 1, 5, 0, 0, DateTimeKind.Utc); // 05:00 UTC is 06:00 local time
            _dateTimeMock.Setup(e => e.UtcNow()).Returns(currentDateTime);

            // Act
            var result = _notificationScheduleService.CanSendEmailNow();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanSendEmailNow_UsesEmailWindowAndNotSmsWindow()
        {
            // Arrange
            var currentDateTime = new DateTime(2022, 1, 1, 14, 30, 0, DateTimeKind.Utc); // 15:30 local time: inside the SMS window (9-17), outside the email window (10-15)
            _dateTimeMock.Setup(e => e.UtcNow()).Returns(currentDateTime);

            // Act / Assert
            Assert.True(_notificationScheduleService.CanSendSmsNow());
            Assert.False(_notificationScheduleService.CanSendEmailNow());
        }

        [Fact]
        public void GetEmailExpirationDateTime_ReferenceTimeIsWithinSendWindow_ReturnsReferenceTimePlus48Hours()
        {
            // Arrange
            var referenceDateTime = new DateTime(2025, 08, 25, 10, 0, 0, DateTimeKind.Utc); // 12:00 local time

            var expectedExpiryDateTime = new DateTime(2025, 08, 27, 10, 0, 0, DateTimeKind.Utc);

            // Act
            var expiryDateTime = _notificationScheduleService.GetEmailExpirationDateTime(referenceDateTime);

            // Assert
            Assert.Equal(expectedExpiryDateTime, expiryDateTime);
        }

        [Fact]
        public void GetEmailExpirationDateTime_WhenReferenceTimeIsAfterSendWindow_ReturnsSendWindowStartThreeDaysAhead()
        {
            // Arrange
            var referenceDateTime = new DateTime(2025, 08, 25, 20, 0, 0, DateTimeKind.Utc); // 22:00 local time

            var expectedExpiryDateTime = new DateTime(2025, 08, 28, 08, 0, 0, DateTimeKind.Utc); // 10:00 local time

            // Act
            var expiryDateTime = _notificationScheduleService.GetEmailExpirationDateTime(referenceDateTime);

            // Assert
            Assert.Equal(expectedExpiryDateTime, expiryDateTime);
        }

        [Fact]
        public void GetEmailExpirationDateTime_WhenReferenceTimeIsBeforeSendWindow_ReturnsSendWindowStartTwoDaysAhead()
        {
            // Arrange
            var referenceDateTime = new DateTime(2025, 08, 25, 05, 0, 0, DateTimeKind.Utc); // 07:00 local time

            var expectedExpiryDateTime = new DateTime(2025, 08, 27, 08, 0, 0, DateTimeKind.Utc); // 10:00 local time

            // Act
            var expiryDateTime = _notificationScheduleService.GetEmailExpirationDateTime(referenceDateTime);

            // Assert
            Assert.Equal(expectedExpiryDateTime, expiryDateTime);
        }

        [Theory]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public void GetEmailExpirationDateTime_WhenReferenceDateTimeIsNotUtc_ThrowsArgumentException(DateTimeKind kind)
        {
            // Arrange
            var nonUtcDateTime = new DateTime(2025, 8, 25, 10, 0, 0, kind);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _notificationScheduleService.GetEmailExpirationDateTime(nonUtcDateTime));
        }
    }
}
