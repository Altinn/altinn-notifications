using System.Collections.Immutable;

using Altinn.Authorization.ProblemDetails;
using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Models.NotificationLog;
using Altinn.Notifications.Validators.Log;

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
    private readonly NotificationLogQueryValidator _validator = new();

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
    public async Task Get_MissingOrgInHttpContext_ReturnsForbidden()
    {
        // Arrange
        var controller = new NotificationLogController(_serviceMock.Object, _validator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var query = new NotificationLogQueryExt { DialogId = "dialog-123" };

        // Act
        var result = await controller.Get(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
        _serviceMock.Verify(s => s.GetByDialogId(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Get_WithNoQueryIdentifiersProvided_ReturnsValidationProblem()
    {
        // Arrange
        var controller = new NotificationLogController(_serviceMock.Object, _validator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { Items = { ["Org"] = "ttd" } }
            }
        };

        var query = new NotificationLogQueryExt { DialogId = null, TransmissionId = null };

        // Act
        var result = await controller.Get(query, TestContext.Current.CancellationToken);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
    }

    [Fact]
    public async Task Get_WithDialogIdOnly_DelegatesGetByDialogId()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new NotificationLogController(_serviceMock.Object, _validator)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        // Act
        var result = await controller.Get(new NotificationLogQueryExt { DialogId = "dialog-123" }, TestContext.Current.CancellationToken);

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

        _serviceMock.Verify(s => s.GetByDialogId("dialog-123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Get_WithTransmissionIdOnly_DelegatesGetByTransmissionId()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new NotificationLogController(_serviceMock.Object, _validator)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        // Act
        var result = await controller.Get(new NotificationLogQueryExt { TransmissionId = "transmission-456" }, TestContext.Current.CancellationToken);

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
        Assert.Equal(_smsLastUpdateTime, smsEntry.LastUpdateTime);
        Assert.Equal(_smsNotificationId, smsEntry.NotificationId);
        Assert.Equal(_smsRequestedSendTime, smsEntry.RequestedSendTime);

        _serviceMock.Verify(s => s.GetByTransmissionId("transmission-456", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Get_WithDialogAndTransmissionIds_DelegatesGetByDialogAndTransmissionIds()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new NotificationLogController(_serviceMock.Object, _validator)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var query = new NotificationLogQueryExt { DialogId = "dialog-123", TransmissionId = "transmission-456" };

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

        _serviceMock.Verify(s => s.GetByDialogAndTransmissionIds("dialog-123", "transmission-456", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Get_WhenServiceReturnsEmptyList_ReturnsOkWithEmptyCollection()
    {
        // Arrange
        var serviceMock = new Mock<INotificationLogService>();
        serviceMock
            .Setup(s => s.GetByDialogId(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableList<NotificationLogSummary>.Empty);

        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new NotificationLogController(serviceMock.Object, _validator)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        // Act
        var result = await controller.Get(new NotificationLogQueryExt { DialogId = "dialog-123" }, TestContext.Current.CancellationToken);

        // Assert
        var actionResult = Assert.IsType<OkObjectResult>(result.Result);
        var notificationLogSummaries = Assert.IsType<IImmutableList<NotificationLogSummaryExt>>(actionResult.Value, exactMatch: false);
        Assert.Empty(notificationLogSummaries);
    }

    [Fact]
    public async Task Get_WhenServiceThrowsOperationCanceledException_Returns499WithProblemDetails()
    {
        // Arrange
        var serviceMock = new Mock<INotificationLogService>();
        serviceMock
            .Setup(s => s.GetByDialogId(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var httpContext = new DefaultHttpContext();
        httpContext.Items["Org"] = "ttd";

        var controller = new NotificationLogController(serviceMock.Object, _validator)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        // Act
        var result = await controller.Get(new NotificationLogQueryExt { DialogId = "dialog-123" }, TestContext.Current.CancellationToken);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(499, statusCodeResult.StatusCode);
        var problemDetails = Assert.IsType<AltinnProblemDetails>(statusCodeResult.Value);
        Assert.Equal("NOT-00002", problemDetails.ErrorCode.ToString());
    }

    [Fact]
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

        var controller = new NotificationLogController(serviceMock.Object, _validator)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        // Act
        await controller.Get(new NotificationLogQueryExt { DialogId = "dialog-123" }, cancellationToken);

        // Assert
        serviceMock.Verify(s => s.GetByDialogId(It.IsAny<string>(), cancellationToken), Times.Once);
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
