using System;

using Altinn.Notifications.Extensions;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingExtensions
{  
    public class DateTimeExtensionsTests
    {
        [Fact]
        public void IsWithinBusinessHours_WithinBusinessHours_ReturnsTrue()
        {
            // Arrange
            var dateTime = new DateTime(2022, 1, 1, 10, 0, 0, DateTimeKind.Utc); // 10:00 UTC is 11:00 or 12:00 local time

            // Act
            var result = dateTime.IsWithinBusinessHours();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsWithinBusinessHours_OutsideBusinessHours_ReturnsFalse()
        {
            // Arrange
            var dateTime = new DateTime(2022, 1, 1, 20, 0, 0, DateTimeKind.Utc); // 20:00 UTC is 21:00 or 22:00 local time

            // Act
            var result = dateTime.IsWithinBusinessHours();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsWithinBusinessHours_UnspecifiedKindTreatedAsUtc_ReturnsExpectedResult()
        {
            // Arrange
            var dateTime = new DateTime(2022, 1, 1, 20, 0, 0, DateTimeKind.Unspecified);

            // Act
            var result = dateTime.IsWithinBusinessHours();

            // Assert
            Assert.False(result);
        }
    }
}
