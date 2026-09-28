using System.Collections.Immutable;

using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Models.NotificationLog;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingControllers;

public class NotificationLogControllerTests
{
    private static readonly Guid _smsNotificationId = Guid.NewGuid();
    private static readonly Guid _emailNotificationId = Guid.NewGuid();
    private static readonly DateTime _smsLastUpdateTime = DateTime.UtcNow.AddMinutes(-2);
    private static readonly DateTime _emailLastUpdateTime = DateTime.UtcNow.AddMinutes(-5);
    private static readonly DateTime _smsRequestedSendTime = DateTime.UtcNow.AddMinutes(-20);
    private static readonly DateTime _emailRequestedSendTime = DateTime.UtcNow.AddMinutes(-30);

    private readonly Mock<INotificationLogService> _serviceMock;
    private readonly Mock<IDialogportenClient> _dialogportenClientMock = new();

    public NotificationLogControllerTests()
    {
        _serviceMock = new Mock<INotificationLogService>();

        _serviceMock
            .Setup(s => s.GetByDialogId(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableList.Create(CreateEmailSummary(), CreateSmsSummary()));

        _serviceMock
            .Setup(s => s.GetByTransmissionId(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableList.Create(CreateEmailSummary(), CreateSmsSummary()));

        _serviceMock
            .Setup(s => s.GetByDialogAndTransmissionIds(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableList.Create(CreateEmailSummary(), CreateSmsSummary()));
    }

    [Fact]
    [Obsolete]
    public async Task Get_MissingOrgInHttpContext_ReturnsForbidden()
    {
        // Arrange
        var controller = new NotificationLogController(_serviceMock.Object, _dialogportenClientMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var query = new NotificationLogQueryExt { DialogId = Guid.NewGuid() };

        // Act
        var result = await controller.Get(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
        _serviceMock.Verify(s => s.GetByDialogId(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Obsolete]
    public async Task Get_WithDialogIdOnly_DelegatesGetByDialogId()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new NotificationLogController(_serviceMock.Object, _dialogportenClientMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        Guid dialogId = Guid.NewGuid();

        // Act
        var result = await controller.Get(new NotificationLogQueryExt { DialogId = dialogId }, TestContext.Current.CancellationToken);

        // Assert
        var actionResult = Assert.IsType<OkObjectResult>(result.Result);
        var notificationLogSummaries = Assert.IsType<IImmutableList<NotificationLogSummaryExt>>(actionResult.Value, exactMatch: false);

        Assert.Equal(2, notificationLogSummaries.Count);

        var emailEntry = Assert.Single(notificationLogSummaries, e => e.Channel == "Email");
        Assert.Equal("Delivered", emailEntry.Status);
        Assert.Equal("Notification", emailEntry.Type);
        Assert.Equal("user@example.com", emailEntry.Destination);
        Assert.Equal(_emailLastUpdateTime, emailEntry.LastUpdateTime);
        Assert.Equal(_emailNotificationId, emailEntry.NotificationId);
        Assert.Equal(_emailRequestedSendTime, emailEntry.RequestedSendTime);

        var smsEntry = Assert.Single(notificationLogSummaries, e => e.Channel == "Sms");
        Assert.Equal("Reminder", smsEntry.Type);
        Assert.Equal("Delivered", smsEntry.Status);
        Assert.Equal("+4799999999", smsEntry.Destination);
        Assert.Equal(_smsLastUpdateTime, smsEntry.LastUpdateTime);
        Assert.Equal(_smsNotificationId, smsEntry.NotificationId);
        Assert.Equal(_smsRequestedSendTime, smsEntry.RequestedSendTime);

        _serviceMock.Verify(s => s.GetByDialogId(dialogId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Obsolete]
    public async Task Get_WithDialogAndTransmissionIds_DelegatesGetByDialogAndTransmissionIds()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new NotificationLogController(_serviceMock.Object, _dialogportenClientMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        Guid dialogId = Guid.NewGuid();
        Guid transmissionId = Guid.NewGuid();

        var query = new NotificationLogQueryExt { DialogId = dialogId, TransmissionId = transmissionId };

        // Act
        var result = await controller.Get(query, TestContext.Current.CancellationToken);

        // Assert
        var actionResult = Assert.IsType<OkObjectResult>(result.Result);
        var notificationLogSummaries = Assert.IsType<IImmutableList<NotificationLogSummaryExt>>(actionResult.Value, exactMatch: false);

        Assert.Equal(2, notificationLogSummaries.Count);

        var emailEntry = Assert.Single(notificationLogSummaries, e => e.Channel == "Email");
        Assert.Equal("Delivered", emailEntry.Status);
        Assert.Equal("Notification", emailEntry.Type);
        Assert.Equal("user@example.com", emailEntry.Destination);
        Assert.Equal(_emailNotificationId, emailEntry.NotificationId);
        Assert.Equal(_emailLastUpdateTime, emailEntry.LastUpdateTime);
        Assert.Equal(_emailRequestedSendTime, emailEntry.RequestedSendTime);

        var smsEntry = Assert.Single(notificationLogSummaries, e => e.Channel == "Sms");
        Assert.Equal("Reminder", smsEntry.Type);
        Assert.Equal("Delivered", smsEntry.Status);
        Assert.Equal("+4799999999", smsEntry.Destination);
        Assert.Equal(_smsNotificationId, smsEntry.NotificationId);
        Assert.Equal(_smsLastUpdateTime, smsEntry.LastUpdateTime);
        Assert.Equal(_smsRequestedSendTime, smsEntry.RequestedSendTime);

        _serviceMock.Verify(s => s.GetByDialogAndTransmissionIds(dialogId.ToString(), transmissionId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Obsolete]
    public async Task Get_WhenServiceReturnsEmptyList_ReturnsOkWithEmptyCollection()
    {
        // Arrange
        var serviceMock = new Mock<INotificationLogService>();
        serviceMock
            .Setup(s => s.GetByDialogId(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableList<NotificationLogSummary>.Empty);

        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new NotificationLogController(serviceMock.Object, _dialogportenClientMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        // Act
        var result = await controller.Get(new NotificationLogQueryExt { DialogId = Guid.NewGuid() }, TestContext.Current.CancellationToken);

        // Assert
        var actionResult = Assert.IsType<OkObjectResult>(result.Result);
        var notificationLogSummaries = Assert.IsType<IImmutableList<NotificationLogSummaryExt>>(actionResult.Value, exactMatch: false);
        Assert.Empty(notificationLogSummaries);
    }

    [Fact]
    [Obsolete]
    public async Task Get_WhenServiceThrowsOperationCanceledException_Returns499WithProblemDetails()
    {
        // Arrange
        var serviceMock = new Mock<INotificationLogService>();
        serviceMock
            .Setup(s => s.GetByDialogId(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new NotificationLogController(serviceMock.Object, _dialogportenClientMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        // Act
        var result = await controller.Get(new NotificationLogQueryExt { DialogId = Guid.NewGuid() }, TestContext.Current.CancellationToken);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(499, statusCodeResult.StatusCode);
        var problemDetails = Assert.IsType<AltinnProblemDetails>(statusCodeResult.Value);
        Assert.Equal("NOT-00002", problemDetails.ErrorCode.ToString());
    }

    [Fact]
    [Obsolete]
    public async Task Get_WithValidRequest_PassesCancellationTokenToService()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var serviceMock = new Mock<INotificationLogService>();
        serviceMock
            .Setup(s => s.GetByDialogId(It.IsAny<string>(), cancellationToken))
            .ReturnsAsync(ImmutableList<NotificationLogSummary>.Empty);

        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new NotificationLogController(serviceMock.Object, _dialogportenClientMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        // Act
        await controller.Get(new NotificationLogQueryExt { DialogId = Guid.NewGuid() }, cancellationToken);

        // Assert
        serviceMock.Verify(s => s.GetByDialogId(It.IsAny<string>(), cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GetForEnduser_WithDialogIdOnlyAndAccessToDialog_DelegatesGetByDialogId()
    {
        // Arrange
        Guid dialogId = Guid.NewGuid();
        _dialogportenClientMock
            .Setup(c => c.CheckUserAccessToDialog(dialogId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new NotificationLogController(_serviceMock.Object, _dialogportenClientMock.Object);

        // Act
        var result = await controller.GetForEnduser(
            new NotificationLogQueryExt { DialogId = dialogId },
            TestContext.Current.CancellationToken);

        // Assert
        var actionResult = Assert.IsType<OkObjectResult>(result.Result);
        var notificationLogSummaries = Assert.IsType<IImmutableList<NotificationLogSummaryExt>>(
            actionResult.Value,
            exactMatch: false);
        Assert.Equal(2, notificationLogSummaries.Count);

        _dialogportenClientMock.Verify(c => c.CheckUserAccessToDialog(dialogId, It.IsAny<CancellationToken>()), Times.Once);
        _serviceMock.Verify(
            s => s.GetByDialogId(dialogId.ToString(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetForEnduser_WithDialogAndTransmissionIdsAndAccessToDialog_DelegatesGetByDialogAndTransmissionIds()
    {
        // Arrange
        Guid dialogId = Guid.NewGuid();
        Guid transmissionId = Guid.NewGuid();
        _dialogportenClientMock
            .Setup(c => c.CheckUserAccessToDialog(dialogId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new NotificationLogController(_serviceMock.Object, _dialogportenClientMock.Object);
        var query = new NotificationLogQueryExt
        {
            DialogId = dialogId,
            TransmissionId = transmissionId
        };

        // Act
        var result = await controller.GetForEnduser(query, TestContext.Current.CancellationToken);

        // Assert
        var actionResult = Assert.IsType<OkObjectResult>(result.Result);
        var notificationLogSummaries = Assert.IsType<IImmutableList<NotificationLogSummaryExt>>(
            actionResult.Value,
            exactMatch: false);
        Assert.Equal(2, notificationLogSummaries.Count);

        _dialogportenClientMock.Verify(c => c.CheckUserAccessToDialog(dialogId, It.IsAny<CancellationToken>()), Times.Once);
        _serviceMock.Verify(
            s => s.GetByDialogAndTransmissionIds(
                dialogId.ToString(),
                transmissionId.ToString(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetForEnduser_WithoutAccessToDialog_ReturnsForbiddenAndDoesNotCallService()
    {
        // Arrange
        Guid dialogId = Guid.NewGuid();
        _dialogportenClientMock
            .Setup(c => c.CheckUserAccessToDialog(dialogId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = new NotificationLogController(_serviceMock.Object, _dialogportenClientMock.Object);

        // Act
        var result = await controller.GetForEnduser(
            new NotificationLogQueryExt { DialogId = dialogId },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
        _dialogportenClientMock.Verify(c => c.CheckUserAccessToDialog(dialogId, It.IsAny<CancellationToken>()), Times.Once);
        _serviceMock.Verify(
            s => s.GetByDialogId(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _serviceMock.Verify(
            s => s.GetByDialogAndTransmissionIds(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetForEnduser_WhenServiceThrowsOperationCanceledException_Returns499WithProblemDetails()
    {
        // Arrange
        Guid dialogId = Guid.NewGuid();
        _dialogportenClientMock
            .Setup(c => c.CheckUserAccessToDialog(dialogId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var serviceMock = new Mock<INotificationLogService>();
        serviceMock
            .Setup(s => s.GetByDialogId(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new NotificationLogController(serviceMock.Object, _dialogportenClientMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        // Act
        var result = await controller.GetForEnduser(new NotificationLogQueryExt { DialogId = dialogId }, TestContext.Current.CancellationToken);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(499, statusCodeResult.StatusCode);
        var problemDetails = Assert.IsType<AltinnProblemDetails>(statusCodeResult.Value);
        Assert.Equal("NOT-00002", problemDetails.ErrorCode.ToString());
    }

    private static NotificationLogSummary CreateSmsSummary() =>
        new()
        {
            Channel = "Sms",
            Type = "Reminder",
            Status = "Delivered",
            Destination = "+4799999999",
            NotificationId = _smsNotificationId,
            LastUpdateTime = _smsLastUpdateTime,
            RequestedSendTime = _smsRequestedSendTime
        };

    private static NotificationLogSummary CreateEmailSummary() =>
        new()
        {
            Channel = "Email",
            Type = "Notification",
            Status = "Delivered",
            Destination = "user@example.com",
            NotificationId = _emailNotificationId,
            LastUpdateTime = _emailLastUpdateTime,
            RequestedSendTime = _emailRequestedSendTime
        };
}
