using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Dashboard;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;

using Moq;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public class DashboardServiceTests
{
    private const string _recipientNin = "12345678901";

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
                [new Recipient { NationalIdentityNumber = _recipientNin }],
                "email",
                "Succeeded",
                null),
        };

        Mock<IDashboardRepository> repository = new();
        repository
            .Setup(x => x.GetDashboardNotificationsByNinAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = new DashboardService(repository.Object);
        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-3);
        DateTimeOffset to = DateTimeOffset.UtcNow;

        // Act
        var result = await sut.GetNotificationsByNinAsync(_recipientNin, from, to, CancellationToken.None);

        // Assert
        repository.Verify(
            x => x.GetDashboardNotificationsByNinAsync(_recipientNin, from, to, It.IsAny<CancellationToken>()),
            Times.Once);

        var entry = Assert.Single(result);
        Assert.Equal(expected[0].NotificationId, entry.NotificationId);
        Assert.Equal(_recipientNin, Assert.Single(entry.Recipients).NationalIdentityNumber);
    }

    [Fact]
    public async Task GetNotificationsByNinAsync_NullDateRange_ForwardsNullsToRepository()
    {
        // Arrange
        Mock<IDashboardRepository> repository = new();
        repository
            .Setup(x => x.GetDashboardNotificationsByNinAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = new DashboardService(repository.Object);

        // Act
        var result = await sut.GetNotificationsByNinAsync(_recipientNin, null, null, CancellationToken.None);

        // Assert
        repository.Verify(
            x => x.GetDashboardNotificationsByNinAsync(_recipientNin, null, null, It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetNotificationsByNinAsync_ForwardsCancellationToken()
    {
        // Arrange
        Mock<IDashboardRepository> repository = new();
        repository
            .Setup(x => x.GetDashboardNotificationsByNinAsync(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<DateTimeOffset?>(),
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
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<DateTimeOffset?>(),
                cts.Token),
            Times.Once);
    }
}
