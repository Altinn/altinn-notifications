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
    public async Task GetByDialogOrTransmission_WhenLookupByNonexistentDialogId_ReturnsEmptyList()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();
        IReadOnlyList<NotificationLogEntry> expected = [];

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogOrTransmission(null, dialogId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result = await _notificationLogService.GetByDialogOrTransmission(null, dialogId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        Assert.Same(expected, result);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByDialogOrTransmission(null, dialogId, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenLookupByValidDialogId_ReturnsNotificationLogEntry()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();
        IReadOnlyList<NotificationLogEntry> expected = CreateNotificationLogEntries();

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogOrTransmission(null, dialogId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result = await _notificationLogService.GetByDialogOrTransmission(null, dialogId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByDialogOrTransmission(null, dialogId, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenLookupByValidSharedDialogId_ReturnsAllNotificationLogEntries()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();

        IReadOnlyList<NotificationLogEntry> expected =
        [
            CreateNotificationLogEntry(),
            CreateNotificationLogEntry(),
            CreateNotificationLogEntry()
        ];

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogOrTransmission(null, dialogId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result =
            await _notificationLogService.GetByDialogOrTransmission(null, dialogId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
        Assert.Equal(3, result.Count);

        _notificationLogRepositoryMock.Verify(repository => repository.GetByDialogOrTransmission(null, dialogId, TestContext.Current.CancellationToken), Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenLookupByValidDialogIdAndRepositoryThrows_PropagatesException()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();
        var expectedException = new InvalidOperationException("Database error");

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogOrTransmission(null, dialogId, TestContext.Current.CancellationToken))
            .ThrowsAsync(expectedException);

        // Act
        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _notificationLogService.GetByDialogOrTransmission(null, dialogId, TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(expectedException, exception);

        _notificationLogRepositoryMock.Verify(
            repository => repository.GetByDialogOrTransmission(null, dialogId, TestContext.Current.CancellationToken),
            Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenLookupByNonexistentTransmissionId_ReturnsEmptyList()
    {
        // Arrange
        string transmissionId = Guid.NewGuid().ToString();
        IReadOnlyList<NotificationLogEntry> expected = [];

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogOrTransmission(transmissionId, null, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result = await _notificationLogService.GetByDialogOrTransmission(transmissionId, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        Assert.Same(expected, result);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByDialogOrTransmission(transmissionId, null, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenLookupByValidTransmissionId_ReturnsNotificationLogEntry()
    {
        // Arrange
        string transmissionId = Guid.NewGuid().ToString();
        IReadOnlyList<NotificationLogEntry> expected = CreateNotificationLogEntries();

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogOrTransmission(transmissionId, null, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result = await _notificationLogService.GetByDialogOrTransmission(transmissionId, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByDialogOrTransmission(transmissionId, null, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenLookupByValidSharedTransmissionId_ReturnsAllNotificationLogEntries()
    {
        // Arrange
        string transmissionId = Guid.NewGuid().ToString();

        IReadOnlyList<NotificationLogEntry> expected =
        [
            CreateNotificationLogEntry(),
            CreateNotificationLogEntry(),
            CreateNotificationLogEntry()
        ];

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogOrTransmission(transmissionId, null, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result =
            await _notificationLogService.GetByDialogOrTransmission(transmissionId, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
        Assert.Equal(3, result.Count);

        _notificationLogRepositoryMock.Verify(repository => repository.GetByDialogOrTransmission(transmissionId, null, TestContext.Current.CancellationToken), Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenLookupByValidTransmissionIdAndRepositoryThrows_PropagatesException()
    {
        // Arrange
        string transmissionId = Guid.NewGuid().ToString();
        var expectedException = new InvalidOperationException("Database error");

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogOrTransmission(transmissionId, null, TestContext.Current.CancellationToken))
            .ThrowsAsync(expectedException);

        // Act
        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _notificationLogService.GetByDialogOrTransmission(transmissionId, null, TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(expectedException, exception);

        _notificationLogRepositoryMock.Verify(
            repository => repository.GetByDialogOrTransmission(transmissionId, null, TestContext.Current.CancellationToken),
            Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenLookupByNonexistentDialogIdAndTransmissionId_ReturnsEmptyList()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();

        IReadOnlyList<NotificationLogEntry> expected = [];

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogOrTransmission(transmissionId, dialogId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result =
            await _notificationLogService.GetByDialogOrTransmission(transmissionId, dialogId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        Assert.Same(expected, result);

        _notificationLogRepositoryMock.Verify(
            repository => repository.GetByDialogOrTransmission(transmissionId, dialogId, TestContext.Current.CancellationToken),
            Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenLookupByValidDialogIdAndTransmissionId_ReturnsNotificationLogEntry()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();

        IReadOnlyList<NotificationLogEntry> expected = CreateNotificationLogEntries();

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogOrTransmission(transmissionId, dialogId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result =
            await _notificationLogService.GetByDialogOrTransmission(transmissionId, dialogId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);

        _notificationLogRepositoryMock.Verify(
            repository => repository.GetByDialogOrTransmission(transmissionId, dialogId, TestContext.Current.CancellationToken),
            Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenLookupByValidSharedDialogIdAndTransmissionId_ReturnsAllNotificationLogEntries()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();

        IReadOnlyList<NotificationLogEntry> expected =
        [
            CreateNotificationLogEntry(),
            CreateNotificationLogEntry(),
            CreateNotificationLogEntry()
        ];

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogOrTransmission(transmissionId, dialogId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result =
            await _notificationLogService.GetByDialogOrTransmission(transmissionId, dialogId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
        Assert.Equal(3, result.Count);

        _notificationLogRepositoryMock.Verify(
            repository => repository.GetByDialogOrTransmission(transmissionId, dialogId, TestContext.Current.CancellationToken),
            Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogOrTransmission_WhenLookupByValidDialogIdAndTransmissionIdAndRepositoryThrows_PropagatesException()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();
        string transmissionId = Guid.NewGuid().ToString();

        var expectedException = new InvalidOperationException("Database error");

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogOrTransmission(transmissionId, dialogId, TestContext.Current.CancellationToken))
            .ThrowsAsync(expectedException);

        // Act
        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _notificationLogService.GetByDialogOrTransmission(
                    transmissionId,
                    dialogId,
                    TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(expectedException, exception);

        _notificationLogRepositoryMock.Verify(
            repository => repository.GetByDialogOrTransmission(transmissionId, dialogId, TestContext.Current.CancellationToken),
            Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    private static NotificationLogEntry CreateNotificationLogEntry()
    {
        return new NotificationLogEntry(
            Recipient: null,
            Channel: "Email",
            CreatorName: "ttd",
            Status: "Delivered",
            Type: "Notification",
            Resource: "resource",
            ShipmentId: Guid.NewGuid(),
            OrderChainId: Guid.NewGuid(),
            NotificationId: Guid.NewGuid(),
            LastUpdateTime: DateTime.UtcNow,
            RequestedSendTime: DateTime.UtcNow,
            DialogId: Guid.NewGuid().ToString(),
            Destination: "recipient@example.com",
            SendersReference: "sender-reference",
            TransmissionId: Guid.NewGuid().ToString(),
            DeliveryReference: Guid.NewGuid().ToString());
    }

    private static IReadOnlyList<NotificationLogEntry> CreateNotificationLogEntries()
    {
        return
        [
            CreateNotificationLogEntry()
        ];
    }
}
