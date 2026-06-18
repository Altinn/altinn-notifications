using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Models.Dashboard;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Models.Dashboard;
using Altinn.Notifications.Validators.Dashboard;

using Microsoft.AspNetCore.Mvc;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingControllers;

public class DashboardControllerTests
{
    private readonly DashboardController _controller;
    private readonly Mock<IDashboardService> _dashboardServiceMock = new();

    public DashboardControllerTests()
    {
        _controller = new DashboardController(_dashboardServiceMock.Object, new NotificationsByNinRequestValidator());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetNotificationsByNin_NinNullEmptyOrWhitespace_ReturnsValidationProblem(string? nin)
    {
        // Act
        var result = await _controller.GetNotificationsByNin(nin!, new NotificationsByNinFiltersExt(), CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        _dashboardServiceMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    public async Task GetNotificationsByNin_NinWrongLength_ReturnsValidationProblem(string nin)
    {
        // Act
        var result = await _controller.GetNotificationsByNin(nin, new NotificationsByNinFiltersExt(), CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        _dashboardServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetNotificationsByNin_FromEqualToTo_ReturnsValidationProblem()
    {
        // Arrange
        var instant = new DateTimeOffset(2026, 05, 01, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = await _controller.GetNotificationsByNin("16069412345", new NotificationsByNinFiltersExt { From = instant, To = instant }, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        _dashboardServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetNotificationsByNin_FromAfterTo_ReturnsValidationProblem()
    {
        // Arrange
        var from = new DateTimeOffset(2026, 05, 10, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 05, 01, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = await _controller.GetNotificationsByNin("16069412345", new NotificationsByNinFiltersExt { From = from, To = to }, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        _dashboardServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetNotificationsByNin_OnlyFromProvided_PassesValidationAndCallsService()
    {
        // Arrange — only one side of the range provided, so the from >= to check must not trigger
        var from = new DateTimeOffset(2026, 05, 01, 0, 0, 0, TimeSpan.Zero);
        Result<List<DashboardNotification>, ServiceError> serviceResult = new List<DashboardNotification>
        {
            new(Guid.NewGuid(), "test", null, null, DateTime.UtcNow, "EmailPreferred", [])
        };
        _dashboardServiceMock
            .Setup(x => x.GetNotificationsByNinAsync("16069412345", from, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _controller.GetNotificationsByNin("16069412345", new NotificationsByNinFiltersExt { From = from }, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetNotificationsByNin_ValidInput_CallsServiceAndReturnsOk()
    {
        // Arrange
        var from = new DateTimeOffset(2026, 05, 01, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 05, 10, 0, 0, 0, TimeSpan.Zero);
        Result<List<DashboardNotification>, ServiceError> serviceResult = new List<DashboardNotification>
        {
            new(Guid.NewGuid(), "test", null, null, DateTime.UtcNow, "EmailPreferred", [])
        };
        _dashboardServiceMock
            .Setup(x => x.GetNotificationsByNinAsync("16069412345", from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _controller.GetNotificationsByNin("16069412345", new NotificationsByNinFiltersExt { From = from, To = to }, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        _dashboardServiceMock.Verify(
            x => x.GetNotificationsByNinAsync("16069412345", from, to, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetNotificationsByNin_NinNotFound_Returns404()
    {
        // Arrange
        Result<List<DashboardNotification>, ServiceError> serviceResult = new ServiceError(404);
        _dashboardServiceMock
            .Setup(x => x.GetNotificationsByNinAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _controller.GetNotificationsByNin("16069412345", new NotificationsByNinFiltersExt(), CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetNotificationsByNin_ToInFuture_ReturnsValidationProblem()
    {
        // Arrange
        var to = DateTime.UtcNow.AddDays(1);

        // Act
        var result = await _controller.GetNotificationsByNin("16069412345", new NotificationsByNinFiltersExt { To = to }, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        _dashboardServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetNotificationsByNin_FromInFuture_ReturnsValidationProblem()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(1);

        // Act
        var result = await _controller.GetNotificationsByNin("16069412345", new NotificationsByNinFiltersExt { From = from }, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        _dashboardServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetNotificationsByNin_FromMoreThan10YearsAgo_ReturnsValidationProblem()
    {
        // Arrange
        var from = DateTime.UtcNow.AddYears(-10).AddDays(-1);

        // Act
        var result = await _controller.GetNotificationsByNin("16069412345", new NotificationsByNinFiltersExt { From = from }, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        _dashboardServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetNotificationsByNin_OnlyToProvidedAndTooFarInPast_ReturnsValidationProblem()
    {
        // Arrange — To is more than 7 days in the past with no From, which the validator rejects
        var to = DateTime.UtcNow.AddDays(-8);

        // Act
        var result = await _controller.GetNotificationsByNin("16069412345", new NotificationsByNinFiltersExt { To = to }, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        _dashboardServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetNotificationsByNin_ServiceThrowsOperationCanceled_Returns499()
    {
        // Arrange
        _dashboardServiceMock
            .Setup(x => x.GetNotificationsByNinAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _controller.GetNotificationsByNin("16069412345", new NotificationsByNinFiltersExt(), CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(499, objectResult.StatusCode);
    }
}
