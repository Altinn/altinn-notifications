using System;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingControllers
{
    public class TriggerControllerTests
    {
        private readonly Mock<ISmsNotificationService> _smsNotificationServiceMock;
        private readonly Mock<INotificationScheduleService> _notificationScheduleMock;
        private readonly TriggerController _controller;

        public TriggerControllerTests()
        {
            _smsNotificationServiceMock = new Mock<ISmsNotificationService>();
            _smsNotificationServiceMock.Setup(x => x.SendNotifications()).Returns(Task.CompletedTask);
            _notificationScheduleMock = new Mock<INotificationScheduleService>();

            _controller = new TriggerController(
                new Mock<IOrderProcessingService>().Object,
                new Mock<IEmailNotificationService>().Object,
                _smsNotificationServiceMock.Object,
                _notificationScheduleMock.Object);
        }

        [Fact]
        public async Task Trigger_SendSmsNotifications_AfterBusinessHours_ServiceNotCalled()
        {
            // Arrange
            var afterBusinessHours = new DateTime(2022, 1, 1, 20, 0, 0, DateTimeKind.Utc);
            _notificationScheduleMock.Setup(x => x.CanSendSmsNotifications()).Returns(false);

            // Act
            ActionResult result = await _controller.Trigger_SendSmsNotifications();

            // Assert
            _smsNotificationServiceMock.Verify(x => x.SendNotifications(), Times.Never);
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task Trigger_SendSmsNotifications_BeforeBusinessHours_ServiceNotCalled()
        {
            // Arrange
            var afterBusinessHours = new DateTime(2022, 1, 1, 04, 0, 0, DateTimeKind.Utc);
            _notificationScheduleMock.Setup(x => x.CanSendSmsNotifications()).Returns(true);

            // Act
            ActionResult result = await _controller.Trigger_SendSmsNotifications();

            // Assert
            _smsNotificationServiceMock.Verify(x => x.SendNotifications(), Times.Never);
            Assert.IsType<OkResult>(result);
        }
    }
}
