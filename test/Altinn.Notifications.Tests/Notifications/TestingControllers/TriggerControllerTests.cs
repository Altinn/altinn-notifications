using System;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Extensions;

using Microsoft.AspNetCore.Mvc;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingControllers
{
    public class TriggerControllerTests
    {
        private readonly Mock<ISmsNotificationService> _smsNotificationServiceMock;
        private readonly Mock<IDateTimeService> _dateTimeServiceMock;
        private readonly TriggerController _controller;

        public TriggerControllerTests()
        {
            _smsNotificationServiceMock = new Mock<ISmsNotificationService>();
            _dateTimeServiceMock = new Mock<IDateTimeService>();

            _controller = new TriggerController(
                new Mock<IOrderProcessingService>().Object,
                new Mock<IEmailNotificationService>().Object,
                _smsNotificationServiceMock.Object,
                _dateTimeServiceMock.Object);
        }

        [Fact]
        public async Task Trigger_SendSmsNotifications_AfterBusinessHours_ServiceNotCalled()
        {
            // Arrange
            var afterBusinessHours = new DateTime(2022, 1, 1, 20, 0, 0, DateTimeKind.Utc);
            _dateTimeServiceMock.Setup(x => x.UtcNow()).Returns(afterBusinessHours);

            // Act
            ActionResult result = await _controller.Trigger_SendSmsNotifications();

            // Assert
            _smsNotificationServiceMock.Verify(x => x.SendNotifications(), Times.Never);
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public void IsWithinBusinessHours_WithinBusinessHours_ReturnsTrue()
        {
            // Arrange
            var dateTime = new DateTime(2022, 1, 1, 10, 0, 0, DateTimeKind.Utc);

            // Act
            var result = dateTime.IsWithinBusinessHours();

            // Assert
            Assert.True(result);
        }
    }
}
