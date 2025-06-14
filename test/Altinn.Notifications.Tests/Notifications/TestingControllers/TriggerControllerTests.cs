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
        private readonly Mock<INotificationScheduleService> _notificationScheduleMock;
        private readonly TriggerController _controller;

        public TriggerControllerTests()
        {
            _smsNotificationServiceMock = new Mock<ISmsNotificationService>();
            _smsNotificationServiceMock.Setup(x => x.SendNotifications(It.IsAny<SendingTimePolicy>())).Returns(Task.CompletedTask);
            _notificationScheduleMock = new Mock<INotificationScheduleService>();

            _controller = new TriggerController(
                new Mock<IOrderProcessingService>().Object,
                new Mock<IEmailNotificationService>().Object,
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
    }
}
