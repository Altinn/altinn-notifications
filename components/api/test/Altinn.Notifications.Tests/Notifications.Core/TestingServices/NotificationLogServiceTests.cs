using System.Collections.Immutable;

using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingServices;

public sealed class NotificationLogServiceTests
{
    private readonly NotificationLogService _notificationLogService;
    private readonly Mock<INotificationLogRepository> _notificationLogRepositoryMock;

    public NotificationLogServiceTests()
    {
        _notificationLogRepositoryMock = new Mock<INotificationLogRepository>();
        _notificationLogService = new NotificationLogService(_notificationLogRepositoryMock.Object);
    }

    [Fact]
    public async Task GetByDialogId_WhenLookupByNonexistentDialogId_ReturnsEmptyList()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();
        IImmutableList<NotificationLogSummary> expected = ImmutableList<NotificationLogSummary>.Empty;

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogId(dialogId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IImmutableList<NotificationLogSummary> result = await _notificationLogService.GetByDialogId(dialogId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        Assert.Same(expected, result);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByDialogId(dialogId, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogId_WhenLookupByValidDialogId_ReturnsNotificationLogEntry()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();
        IImmutableList<NotificationLogSummary> expected = CreateNotificationLogEntries();

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogId(dialogId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IImmutableList<NotificationLogSummary> result = await _notificationLogService.GetByDialogId(dialogId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByDialogId(dialogId, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogId_WhenLookupByValidSharedDialogId_ReturnsAllNotificationLogEntries()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();

        IImmutableList<NotificationLogSummary> expected = ImmutableList.Create(
            CreateNotificationLogEntry(),
            CreateNotificationLogEntry(),
            CreateNotificationLogEntry());

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogId(dialogId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IImmutableList<NotificationLogSummary> result = await _notificationLogService.GetByDialogId(dialogId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
        Assert.Equal(3, result.Count);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByDialogId(dialogId, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogId_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();
        var expectedException = new InvalidOperationException("Database error");

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogId(dialogId, TestContext.Current.CancellationToken))
            .ThrowsAsync(expectedException);

        // Act
        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _notificationLogService.GetByDialogId(dialogId, TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(expectedException, exception);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByDialogId(dialogId, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByTransmissionId_WhenLookupByNonexistentTransmissionId_ReturnsEmptyList()
    {
        // Arrange
        string transmissionId = Guid.NewGuid().ToString();
        IImmutableList<NotificationLogSummary> expected = ImmutableList<NotificationLogSummary>.Empty;

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IImmutableList<NotificationLogSummary> result = await _notificationLogService.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        Assert.Same(expected, result);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByTransmissionId_WhenLookupByValidTransmissionId_ReturnsNotificationLogEntry()
    {
        // Arrange
        string transmissionId = Guid.NewGuid().ToString();
        IImmutableList<NotificationLogSummary> expected = CreateNotificationLogEntries();

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IImmutableList<NotificationLogSummary> result = await _notificationLogService.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByTransmissionId_WhenLookupByValidSharedTransmissionId_ReturnsAllNotificationLogEntries()
    {
        // Arrange
        string transmissionId = Guid.NewGuid().ToString();

        IImmutableList<NotificationLogSummary> expected = ImmutableList.Create(
            CreateNotificationLogEntry(),
            CreateNotificationLogEntry(),
            CreateNotificationLogEntry());

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IImmutableList<NotificationLogSummary> result = await _notificationLogService.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
        Assert.Equal(3, result.Count);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByTransmissionId_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        string transmissionId = Guid.NewGuid().ToString();
        var expectedException = new InvalidOperationException("Database error");

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken))
            .ThrowsAsync(expectedException);

        // Act
        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _notificationLogService.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(expectedException, exception);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogAndTransmissionIds_WhenLookupByNonexistentIds_ReturnsEmptyList()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();
        IImmutableList<NotificationLogSummary> expected = ImmutableList<NotificationLogSummary>.Empty;

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogAndTransmissionIds(dialogId, transmissionId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IImmutableList<NotificationLogSummary> result =
            await _notificationLogService.GetByDialogAndTransmissionIds(dialogId, transmissionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        Assert.Same(expected, result);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByDialogAndTransmissionIds(dialogId, transmissionId, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogAndTransmissionIds_WhenLookupByValidIds_ReturnsNotificationLogEntry()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();
        IImmutableList<NotificationLogSummary> expected = CreateNotificationLogEntries();

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogAndTransmissionIds(dialogId, transmissionId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IImmutableList<NotificationLogSummary> result =
            await _notificationLogService.GetByDialogAndTransmissionIds(dialogId, transmissionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByDialogAndTransmissionIds(dialogId, transmissionId, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogAndTransmissionIds_WhenLookupByValidSharedIds_ReturnsAllNotificationLogEntries()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();

        IImmutableList<NotificationLogSummary> expected = ImmutableList.Create(
            CreateNotificationLogEntry(),
            CreateNotificationLogEntry(),
            CreateNotificationLogEntry());

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogAndTransmissionIds(dialogId, transmissionId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IImmutableList<NotificationLogSummary> result =
            await _notificationLogService.GetByDialogAndTransmissionIds(dialogId, transmissionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
        Assert.Equal(3, result.Count);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByDialogAndTransmissionIds(dialogId, transmissionId, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogAndTransmissionIds_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();
        var expectedException = new InvalidOperationException("Database error");

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogAndTransmissionIds(dialogId, transmissionId, TestContext.Current.CancellationToken))
            .ThrowsAsync(expectedException);

        // Act
        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _notificationLogService.GetByDialogAndTransmissionIds(dialogId, transmissionId, TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(expectedException, exception);
        _notificationLogRepositoryMock.Verify(
            repository => repository.GetByDialogAndTransmissionIds(dialogId, transmissionId, TestContext.Current.CancellationToken),
            Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    private static NotificationLogSummary CreateNotificationLogEntry()
    {
        return new NotificationLogSummary
        {
            NotificationId = Guid.NewGuid(),
            DialogId = Guid.NewGuid().ToString(),
            TransmissionId = Guid.NewGuid().ToString(),
            Type = "Notification",
            Channel = "Email",
            Destination = "recipient@example.com",
            Status = "Delivered",
            RequestedSendTime = DateTime.UtcNow,
            LastUpdateTime = DateTime.UtcNow
        };
    }

    private static ImmutableList<NotificationLogSummary> CreateNotificationLogEntries()
    {
        return ImmutableList.Create(CreateNotificationLogEntry());
    }
}
