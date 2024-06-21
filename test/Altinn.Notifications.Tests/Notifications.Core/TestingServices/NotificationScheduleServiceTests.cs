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
                SmsSendWindowEndHour = 17
            };
            _notificationScheduleService = new(_dateTimeMock.Object, Options.Create(config));
        }

        [Fact]
        public void CanSendSmsNotifications_WithinBusinessHours_ReturnsTrue()
        {
            // Arrange
            var dateTime = new DateTime(2022, 1, 1, 10, 0, 0, DateTimeKind.Utc); // 10:00 UTC is 11:00 or 12:00 local time
            _dateTimeMock.Setup(m => m.UtcNow()).Returns(dateTime);

            // Act
            var result = _notificationScheduleService.CanSendSmsNotifications();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanSendSmsNotifications_AfterBusinessHours_ReturnsFalse()
        {
            // Arrange
            var dateTime = new DateTime(2022, 1, 1, 20, 0, 0, DateTimeKind.Utc); // 20:00 UTC is 21:00 or 22:00 local time
            _dateTimeMock.Setup(m => m.UtcNow()).Returns(dateTime);

            // Act
            var result = _notificationScheduleService.CanSendSmsNotifications();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanSendSmsNotifications_BeforeBusinessHours_ReturnsFalse()
        {
            // Arrange
            var dateTime = new DateTime(2022, 1, 1, 5, 0, 0, DateTimeKind.Utc); // 05:00 UTC is 07:00 or 08:00 local time
            _dateTimeMock.Setup(m => m.UtcNow()).Returns(dateTime);

            // Act
            var result = _notificationScheduleService.CanSendSmsNotifications();

            // Assert
            Assert.False(result);
        }

    }
}
