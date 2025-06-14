using System;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingControllers
{
    public class TriggerControllerTests
    {
        private readonly Mock<ISmsNotificationService> _smsNotificationServiceMock;
        private readonly Mock<IEmailNotificationService> _emailNotificationServiceMock;
        private readonly Mock<IOrderProcessingService> _orderProcessingServiceMock;
        private readonly Mock<INotificationScheduleService> _notificationScheduleMock;
        private readonly TriggerController _controller;

        public TriggerControllerTests()
        {
            _smsNotificationServiceMock = new Mock<ISmsNotificationService>();
            _emailNotificationServiceMock = new Mock<IEmailNotificationService>();
            _orderProcessingServiceMock = new Mock<IOrderProcessingService>();
            _notificationScheduleMock = new Mock<INotificationScheduleService>();

            _controller = new TriggerController(
                _orderProcessingServiceMock.Object,
                _emailNotificationServiceMock.Object,
                _smsNotificationServiceMock.Object,
                _notificationScheduleMock.Object,
                NullLogger<TriggerController>.Instance);
        }

        [Fact]
        public async Task Trigger_SendSmsNotifications_AfterBusinessHours_ServiceNotCalled()
        {
            // Arrange
            _notificationScheduleMock.Setup(x => x.CanSendSmsNotifications()).Returns(false);

            // Act
            ActionResult result = await _controller.Trigger_SendSmsNotificationsDaytime();

            // Assert
            _smsNotificationServiceMock.Verify(x => x.SendNotifications(It.IsAny<SendingTimePolicy>()), Times.Never);
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task Trigger_SendSmsNotifications_BeforeBusinessHours_ServiceNotCalled()
        {
            // Arrange
            _notificationScheduleMock.Setup(x => x.CanSendSmsNotifications()).Returns(false);

            // Act
            ActionResult result = await _controller.Trigger_SendSmsNotificationsDaytime();

            // Assert
            _smsNotificationServiceMock.Verify(x => x.SendNotifications(It.IsAny<SendingTimePolicy>()), Times.Never);
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task Trigger_TerminateExpiredNotifications_Success()
        {
            // Arrange
            _emailNotificationServiceMock.Setup(x => x.TerminateExpiredNotifications()).Returns(Task.CompletedTask);
            _smsNotificationServiceMock.Setup(x => x.TerminateExpiredNotifications()).Returns(Task.CompletedTask);

            // Act
            IActionResult result = await _controller.Trigger_TerminateExpiredNotifications();

            // Assert
            _emailNotificationServiceMock.Verify(x => x.TerminateExpiredNotifications(), Times.Once);
            _smsNotificationServiceMock.Verify(x => x.TerminateExpiredNotifications(), Times.Once);
            Assert.IsType<OkResult>(result);
        }

        [Fact]
        public async Task Trigger_TerminateExpiredNotifications_EmailServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            _emailNotificationServiceMock.Setup(x => x.TerminateExpiredNotifications()).ThrowsAsync(new Exception("Simulated exception"));
            _smsNotificationServiceMock.Setup(x => x.TerminateExpiredNotifications()).Returns(Task.CompletedTask);

            // Act
            IActionResult result = await _controller.Trigger_TerminateExpiredNotifications();

            // Assert
            _smsNotificationServiceMock.Verify(x => x.TerminateExpiredNotifications(), Times.Never);
            _emailNotificationServiceMock.Verify(x => x.TerminateExpiredNotifications(), Times.Once);
            Assert.IsType<ObjectResult>(result);
            var statusCodeResult = (ObjectResult)result;
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task Trigger_TerminateExpiredNotifications_SmsServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            _emailNotificationServiceMock.Setup(x => x.TerminateExpiredNotifications()).Returns(Task.CompletedTask);
            _smsNotificationServiceMock.Setup(x => x.TerminateExpiredNotifications()).ThrowsAsync(new Exception("Simulated exception"));

            // Act
            IActionResult result = await _controller.Trigger_TerminateExpiredNotifications();

            // Assert
            _emailNotificationServiceMock.Verify(x => x.TerminateExpiredNotifications(), Times.Once);
            _smsNotificationServiceMock.Verify(x => x.TerminateExpiredNotifications(), Times.Once);
            Assert.IsType<ObjectResult>(result);
            var statusCodeResult = (ObjectResult)result;
            Assert.Equal(500, statusCodeResult.StatusCode);
        }
    }
}
