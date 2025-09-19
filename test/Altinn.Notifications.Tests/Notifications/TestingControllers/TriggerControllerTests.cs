using System;
using System.Diagnostics.Metrics;
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

    private readonly Mock<ISmsNotificationService> _smsNotificationServiceMock = new();
    private readonly Mock<IOrderProcessingService> _orderProcessingServiceMock = new();
    private readonly Mock<INotificationScheduleService> _notificationScheduleMock = new();
    private readonly Mock<IEmailNotificationService> _emailNotificationServiceMock = new();
    private readonly Mock<IStatusFeedService> _statusFeedServiceMock = new();
    private readonly Mock<IMetricsService> _metricsServiceMock = new();
    private readonly Meter _testMeter = new Meter("test-meter");
    private readonly Counter<long> _testCounter;

    public TriggerControllerTests()
    {
        _controller = new TriggerController(
            _orderProcessingServiceMock.Object,
            _emailNotificationServiceMock.Object,
            _smsNotificationServiceMock.Object,
            _notificationScheduleMock.Object,
            _statusFeedServiceMock.Object,
            _metricsServiceMock.Object,
            NullLogger<TriggerController>.Instance);
    
        _testCounter = _testMeter.CreateCounter<long>("test-counter");
    }

    [Fact]
    public async Task Trigger_SendSmsNotificationsDaytime_CanSendSmsNowReturnsFalse_ServiceNotCalled()
    {
        // Arrange
        _notificationScheduleMock.Setup(x => x.CanSendSmsNow()).Returns(false);
        _metricsServiceMock.Setup(x => x.TriggerSendSmsNotificationsDaytimeCounter).Returns(_testCounter);

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
        _smsNotificationServiceMock.Setup(x => x.TerminateExpiredNotifications()).Returns(Task.CompletedTask);
        _emailNotificationServiceMock.Setup(x => x.TerminateExpiredNotifications()).Returns(Task.CompletedTask);
        _metricsServiceMock.Setup(x => x.TriggerTerminateExpiredNotificationsCounter).Returns(_testCounter);

        // Act
        IActionResult result = await _controller.Trigger_TerminateExpiredNotifications();

        // Assert
        _smsNotificationServiceMock.Verify(x => x.TerminateExpiredNotifications(), Times.Once);
        _emailNotificationServiceMock.Verify(x => x.TerminateExpiredNotifications(), Times.Once);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Trigger_TerminateExpiredNotifications_EmailServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _smsNotificationServiceMock.Setup(x => x.TerminateExpiredNotifications()).Returns(Task.CompletedTask);
        _emailNotificationServiceMock.Setup(x => x.TerminateExpiredNotifications()).ThrowsAsync(new Exception("Simulated exception"));
        _metricsServiceMock.Setup(x => x.TriggerTerminateExpiredNotificationsCounter).Returns(_testCounter);

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
    public async Task Trigger_TerminateExpiredNotifications_SmsServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _emailNotificationServiceMock.Setup(x => x.TerminateExpiredNotifications()).Returns(Task.CompletedTask);
        _smsNotificationServiceMock.Setup(x => x.TerminateExpiredNotifications()).ThrowsAsync(new Exception("Simulated exception"));
        _metricsServiceMock.Setup(x => x.TriggerTerminateExpiredNotificationsCounter).Returns(_testCounter);

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
    public async Task Trigger_DeleteOldStatusFeedRecords_CallsServiceAndReturnsOk()
    {
        // Arrange
        _statusFeedServiceMock.Setup(x => x.DeleteOldStatusFeedRecords(CancellationToken.None)).Returns(Task.CompletedTask);
        _metricsServiceMock.Setup(x => x.TriggerDeleteOldStatusFeedRecords).Returns(_testCounter);

        // Act
        var result = await _controller.Trigger_DeleteOldStatusFeedRecords(CancellationToken.None);

        // Assert
        _statusFeedServiceMock.Verify(x => x.DeleteOldStatusFeedRecords(CancellationToken.None), Times.Once);
        Assert.IsType<OkResult>(result);
    }
}
