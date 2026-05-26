using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Controllers;
using Altinn.Notifications.Core.Models.Dashboard;
using Altinn.Notifications.Core.Services.Interfaces;

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
        _controller = new DashboardController(_dashboardServiceMock.Object);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetNotificationsByNin_NinNullEmptyOrWhitespace_ReturnsBadRequest(string? nin)
    {
        // Act
        var result = await _controller.GetNotificationsByNin(nin!, from: null, to: null, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("'nin' is required and cannot be empty", badRequest.Value);
        _dashboardServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetNotificationsByNin_FromEqualToTo_ReturnsBadRequest()
    {
        // Arrange
        var instant = new DateTimeOffset(2026, 05, 01, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = await _controller.GetNotificationsByNin("16069412345", instant, instant, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("'from' must be earlier than 'to'.", badRequest.Value);
        _dashboardServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetNotificationsByNin_FromAfterTo_ReturnsBadRequest()
    {
        // Arrange
        var from = new DateTimeOffset(2026, 05, 10, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 05, 01, 0, 0, 0, TimeSpan.Zero);

        // Act
        var result = await _controller.GetNotificationsByNin("16069412345", from, to, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("'from' must be earlier than 'to'.", badRequest.Value);
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
        var result = await _controller.GetNotificationsByNin("16069412345", from, to: null, CancellationToken.None);

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
        var result = await _controller.GetNotificationsByNin("16069412345", from, to, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        _dashboardServiceMock.Verify(
            x => x.GetNotificationsByNinAsync("16069412345", from, to, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
