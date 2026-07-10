using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Dashboard;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Shared;

using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class DashboardServiceTests
{
    private const string _recipientNin = "12345678901";
    private const string _recipientOrgNo = "123456789";

    [Fact]
    public async Task GetNotificationsByNinAsync_ValidInput_ReturnsRepositoryResult()
    {
        // Arrange
        var expected = new List<DashboardNotification>
        {
            new(
                Guid.NewGuid(),
                "test",
                null,
                null,
                DateTime.UtcNow,
                NotificationChannel.EmailPreferred,
                "notification",
                [new DashboardDeliveryAttempt(_recipientNin, null, "email", null, null, "Succeeded", null)]),
        };

        Mock<IDashboardRepository> repository = new();
        repository
            .Setup(x => x.GetDashboardNotificationsByNinAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = new DashboardService(repository.Object);
        DateTime from = DateTime.UtcNow.AddDays(-3);
        DateTime to = DateTime.UtcNow;

        // Act
        var result = await sut.GetNotificationsByNinAsync(_recipientNin, from, to, CancellationToken.None);

        // Assert
        repository.Verify(
            x => x.GetDashboardNotificationsByNinAsync(_recipientNin, from, to, It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(result.Value!);
        Assert.Equal(expected[0].ShipmentId, entry.ShipmentId);
        Assert.Equal(_recipientNin, Assert.Single(entry.DeliveryAttempts).NationalIdentityNumber);
    }

    [Fact]
    public async Task GetNotificationsByNinAsync_NullDateRange_ForwardsNullsToRepository()
    {
        // Arrange
        Mock<IDashboardRepository> repository = new();
        repository
            .Setup(x => x.GetDashboardNotificationsByNinAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = new DashboardService(repository.Object);

        // Act
        var result = await sut.GetNotificationsByNinAsync(_recipientNin, null, null, CancellationToken.None);

        // Assert
        repository.Verify(
            x => x.GetDashboardNotificationsByNinAsync(_recipientNin, null, null, It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetNotificationsByNinAsync_ForwardsCancellationToken()
    {
        // Arrange
        Mock<IDashboardRepository> repository = new();
        repository
            .Setup(x => x.GetDashboardNotificationsByNinAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = new DashboardService(repository.Object);
        using var cts = new CancellationTokenSource();

        // Act
        await sut.GetNotificationsByNinAsync(_recipientNin, null, null, cts.Token);

        // Assert
        repository.Verify(
            x => x.GetDashboardNotificationsByNinAsync(
                _recipientNin,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task GetNotificationsByOrgNumberAsync_ValidInput_ReturnsRepositoryResult()
    {
        // Arrange
        var expected = new List<DashboardNotification>
        {
            new(
                Guid.NewGuid(),
                "test",
                null,
                null,
                DateTime.UtcNow,
                NotificationChannel.EmailPreferred,
                "notification",
                [new DashboardDeliveryAttempt(null, _recipientOrgNo, "email", null, null, "Succeeded", null)]),
        };

        Mock<IDashboardRepository> repository = new();
        repository
            .Setup(x => x.GetDashboardNotificationsByOrgNumberAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = new DashboardService(repository.Object);
        DateTime from = DateTime.UtcNow.AddDays(-3);
        DateTime to = DateTime.UtcNow;

        // Act
        var result = await sut.GetNotificationsByOrgNumberAsync(_recipientOrgNo, from, to, CancellationToken.None);

        // Assert
        repository.Verify(
            x => x.GetDashboardNotificationsByOrgNumberAsync(_recipientOrgNo, from, to, It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(result.Value!);
        Assert.Equal(expected[0].ShipmentId, entry.ShipmentId);
        Assert.Equal(_recipientOrgNo, Assert.Single(entry.DeliveryAttempts).OrganizationNumber);
    }

    [Fact]
    public async Task GetNotificationsByOrgNumberAsync_NullDateRange_ForwardsNullsToRepository()
    {
        // Arrange
        Mock<IDashboardRepository> repository = new();
        repository
            .Setup(x => x.GetDashboardNotificationsByOrgNumberAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = new DashboardService(repository.Object);

        // Act
        var result = await sut.GetNotificationsByOrgNumberAsync(_recipientOrgNo, null, null, CancellationToken.None);

        // Assert
        repository.Verify(
            x => x.GetDashboardNotificationsByOrgNumberAsync(_recipientOrgNo, null, null, It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetNotificationsByOrgNumberAsync_ForwardsCancellationToken()
    {
        // Arrange
        Mock<IDashboardRepository> repository = new();
        repository
            .Setup(x => x.GetDashboardNotificationsByOrgNumberAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = new DashboardService(repository.Object);
        using var cts = new CancellationTokenSource();

        // Act
        await sut.GetNotificationsByOrgNumberAsync(_recipientOrgNo, null, null, cts.Token);

        // Assert
        repository.Verify(
            x => x.GetDashboardNotificationsByOrgNumberAsync(
                _recipientOrgNo,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                cts.Token),
            Times.Once);
    }
}
