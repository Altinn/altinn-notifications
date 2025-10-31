using System;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.BackgroundQueue;
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
    private readonly Mock<ISmsPublishTaskQueue> _smsPublishTaskQueueMock = new();
    private readonly Mock<IEmailPublishTaskQueue> _emailPublishTaskQueueMock = new();
    private readonly Mock<ISmsNotificationService> _smsNotificationServiceMock = new();
    private readonly Mock<IOrderProcessingService> _orderProcessingServiceMock = new();
    private readonly Mock<INotificationScheduleService> _notificationScheduleMock = new();
    private readonly Mock<IEmailNotificationService> _emailNotificationServiceMock = new();

    public TriggerControllerTests()
    {
        _controller = new TriggerController(
            NullLogger<TriggerController>.Instance,
            _statusFeedServiceMock.Object,
            _smsPublishTaskQueueMock.Object,
            _emailPublishTaskQueueMock.Object,
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
}
