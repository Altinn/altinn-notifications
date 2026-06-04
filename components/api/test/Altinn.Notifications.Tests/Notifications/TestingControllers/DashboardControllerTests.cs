using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Models.Dashboard;
using Altinn.Notifications.Core.Services.Interfaces;
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
        _controller = new DashboardController(_dashboardServiceMock.Object, new GetNotificationsByNinRequestValidator());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetNotificationsByNin_NinNullEmptyOrWhitespace_ReturnsValidationProblem(string? nin)
    {
        // Act
        var result = await _controller.GetNotificationsByNin(new GetNotificationsByNinRequestExt { Nin = nin! }, CancellationToken.None);

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
        var result = await _controller.GetNotificationsByNin(new GetNotificationsByNinRequestExt { Nin = "16069412345", From = instant, To = instant }, CancellationToken.None);

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
        var result = await _controller.GetNotificationsByNin(new GetNotificationsByNinRequestExt { Nin = "16069412345", From = from, To = to }, CancellationToken.None);

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
        _dashboardServiceMock
            .Setup(x => x.GetNotificationsByNinAsync("16069412345", from, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DashboardNotification>());

        // Act
        var result = await _controller.GetNotificationsByNin(new GetNotificationsByNinRequestExt { Nin = "16069412345", From = from }, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetNotificationsByNin_ValidInput_CallsServiceAndReturnsOk()
    {
        // Arrange
        var from = new DateTimeOffset(2026, 05, 01, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 05, 10, 0, 0, 0, TimeSpan.Zero);
        _dashboardServiceMock
            .Setup(x => x.GetNotificationsByNinAsync("16069412345", from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DashboardNotification>());

        // Act
        var result = await _controller.GetNotificationsByNin(new GetNotificationsByNinRequestExt { Nin = "16069412345", From = from, To = to }, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        _dashboardServiceMock.Verify(
            x => x.GetNotificationsByNinAsync("16069412345", from, to, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
