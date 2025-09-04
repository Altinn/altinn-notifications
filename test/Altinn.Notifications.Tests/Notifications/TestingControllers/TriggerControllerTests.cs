using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingControllers;

public class TriggerControllerTests
{
    private readonly TriggerController _controller;

    private readonly Mock<IStatusFeedService> _statusFeedServiceMock = new();
    private readonly Mock<ISmsNotificationService> _smsNotificationServiceMock = new();
    private readonly Mock<IOrderProcessingService> _orderProcessingServiceMock = new();
    private readonly Mock<INotificationScheduleService> _notificationScheduleMock = new();
    private readonly Mock<IEmailNotificationService> _emailNotificationServiceMock = new();

    public TriggerControllerTests()
    {
        _controller = new TriggerController(
            NullLogger<TriggerController>.Instance,
            _statusFeedServiceMock.Object,
            _notificationScheduleMock.Object,
            _orderProcessingServiceMock.Object,
            _smsNotificationServiceMock.Object,
            _emailNotificationServiceMock.Object);
    }

    [Fact]
    public async Task Trigger_DeleteOldStatusFeedRecords_CallsServiceAndReturnsOk()
    {
        // Arrange
        _statusFeedServiceMock.Setup(x => x.DeleteOldStatusFeedRecords(CancellationToken.None)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Trigger_DeleteOldStatusFeedRecords(CancellationToken.None);

        // Assert
        _statusFeedServiceMock.Verify(x => x.DeleteOldStatusFeedRecords(CancellationToken.None), Times.Once);
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Trigger_TerminateExpiredNotifications_Success()
    {
        // Arrange
        _smsNotificationServiceMock.Setup(x => x.TerminateExpiredNotifications()).Returns(Task.CompletedTask);
        _emailNotificationServiceMock.Setup(x => x.TerminateExpiredNotifications()).Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _controller.Trigger_TerminateExpiredNotifications();

        // Assert
        _smsNotificationServiceMock.Verify(x => x.TerminateExpiredNotifications(), Times.Once);
        _emailNotificationServiceMock.Verify(x => x.TerminateExpiredNotifications(), Times.Once);

        Assert.IsType<OkResult>(result);
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
        _smsNotificationServiceMock.Verify(x => x.TerminateExpiredNotifications(), Times.Once);
        _emailNotificationServiceMock.Verify(x => x.TerminateExpiredNotifications(), Times.Once);

        Assert.IsType<ObjectResult>(result);
        var statusCodeResult = (ObjectResult)result;
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task Trigger_TerminateExpiredNotifications_EmailServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _smsNotificationServiceMock.Setup(x => x.TerminateExpiredNotifications()).Returns(Task.CompletedTask);
        _emailNotificationServiceMock.Setup(x => x.TerminateExpiredNotifications()).ThrowsAsync(new Exception("Simulated exception"));

        // Act
        IActionResult result = await _controller.Trigger_TerminateExpiredNotifications();

        // Assert
        _smsNotificationServiceMock.Verify(x => x.TerminateExpiredNotifications(), Times.Never);
        _emailNotificationServiceMock.Verify(x => x.TerminateExpiredNotifications(), Times.Once);

        Assert.IsType<ObjectResult>(result);
        var statusCodeResult = (ObjectResult)result;
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsDaytime_ServiceThrowsException_PropagatesException()
    {
        // Arrange
        _notificationScheduleMock.Setup(e => e.CanSendSmsNow()).Returns(true);

        _smsNotificationServiceMock
            .Setup(e => e.SendNotifications(It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .ThrowsAsync(new Exception("Test exception"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _controller.Trigger_SendSmsNotificationsDaytime());
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsDaytime_CanSendSmsNowReturnsFalse_ServiceNotCalled()
    {
        // Arrange
        _notificationScheduleMock.Setup(e => e.CanSendSmsNow()).Returns(false);

        // Act
        ActionResult result = await _controller.Trigger_SendSmsNotificationsDaytime(CancellationToken.None);

        // Assert
        Assert.IsType<OkResult>(result);
        _smsNotificationServiceMock.Verify(x => x.SendNotifications(It.IsAny<CancellationToken>(), It.IsAny<SendingTimePolicy>()), Times.Never);
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsDaytime_DuringBusinessHours_CallsServiceAndReturnsOk()
    {
        // Arrange
        _notificationScheduleMock.Setup(s => s.CanSendSmsNow()).Returns(true);

        _smsNotificationServiceMock
            .Setup(e => e.SendNotifications(It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Trigger_SendSmsNotificationsDaytime();

        // Assert
        Assert.IsType<OkResult>(result);
        _smsNotificationServiceMock.Verify(s => s.SendNotifications(It.IsAny<CancellationToken>(), SendingTimePolicy.Daytime), Times.Once);
    }
}
