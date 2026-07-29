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
    public async Task GetByDialogId_WithValidDialogId_ReturnsNotificationLogEntries()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();
        IReadOnlyList<NotificationLogEntry> expected = CreateNotificationLogEntries();

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogId(dialogId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result = await _notificationLogService.GetByDialogId(dialogId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByDialogId(dialogId, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogId_WhenRepositoryReturnsEmptyList_ReturnsEmptyList()
    {
        // Arrange
        string dialogId = Guid.NewGuid().ToString();
        IReadOnlyList<NotificationLogEntry> expected = [];

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByDialogId(dialogId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result = await _notificationLogService.GetByDialogId(dialogId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        Assert.Same(expected, result);
        _notificationLogRepositoryMock.Verify(repository => repository.GetByDialogId(dialogId, TestContext.Current.CancellationToken), Times.Once);
        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByDialogId_WhenRepositoryReturnsMultipleEntries_ReturnsAllNotificationLogEntries()
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
            .Setup(repository => repository.GetByDialogId(dialogId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result =
            await _notificationLogService.GetByDialogId(dialogId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
        Assert.Equal(3, result.Count);

        _notificationLogRepositoryMock.Verify(repository => repository.GetByDialogId(dialogId, TestContext.Current.CancellationToken), Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByShipmentId_WithValidShipmentId_ReturnsNotificationLogEntries()
    {
        // Arrange
        Guid shipmentId = Guid.NewGuid();
        IReadOnlyList<NotificationLogEntry> expected = CreateNotificationLogEntries();

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByShipmentId(shipmentId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result =
            await _notificationLogService.GetByShipmentId(shipmentId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);

        _notificationLogRepositoryMock.Verify(
            repository => repository.GetByShipmentId(shipmentId, TestContext.Current.CancellationToken),
            Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByShipmentId_WhenRepositoryReturnsEmptyList_ReturnsEmptyList()
    {
        // Arrange
        Guid shipmentId = Guid.NewGuid();
        IReadOnlyList<NotificationLogEntry> expected = [];

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByShipmentId(shipmentId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result =
            await _notificationLogService.GetByShipmentId(shipmentId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        Assert.Same(expected, result);

        _notificationLogRepositoryMock.Verify(
            repository => repository.GetByShipmentId(shipmentId, TestContext.Current.CancellationToken),
            Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByShipmentId_WhenRepositoryReturnsMultipleEntries_ReturnsAllNotificationLogEntries()
    {
        // Arrange
        Guid shipmentId = Guid.NewGuid();

        IReadOnlyList<NotificationLogEntry> expected =
        [
            CreateNotificationLogEntry(),
            CreateNotificationLogEntry(),
            CreateNotificationLogEntry()
        ];

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByShipmentId(shipmentId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result =
            await _notificationLogService.GetByShipmentId(shipmentId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
        Assert.Equal(3, result.Count);

        _notificationLogRepositoryMock.Verify(repository => repository.GetByShipmentId(shipmentId, TestContext.Current.CancellationToken), Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByTransmissionId_WithValidTransmissionId_ReturnsNotificationLogEntries()
    {
        // Arrange
        string transmissionId = Guid.NewGuid().ToString();
        IReadOnlyList<NotificationLogEntry> expected = CreateNotificationLogEntries();

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result =
            await _notificationLogService.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);

        _notificationLogRepositoryMock.Verify(repository => repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken), Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByTransmissionId_WithEmptyString_ReturnsEmptyList()
    {
        // Arrange
        string transmissionId = string.Empty;
        IReadOnlyList<NotificationLogEntry> expected = [];

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result =
            await _notificationLogService.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);

        _notificationLogRepositoryMock.Verify(repository => repository.GetByTransmissionId(string.Empty, TestContext.Current.CancellationToken), Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByTransmissionId_WhenRepositoryReturnsEmptyList_ReturnsEmptyList()
    {
        // Arrange
        string transmissionId = Guid.NewGuid().ToString();
        IReadOnlyList<NotificationLogEntry> expected = [];

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result =
            await _notificationLogService.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
        Assert.Same(expected, result);

        _notificationLogRepositoryMock.Verify(
            repository => repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken),
            Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByTransmissionId_WhenRepositoryReturnsMultipleEntries_ReturnsAllEntries()
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
            .Setup(repository => repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);

        // Act
        IReadOnlyList<NotificationLogEntry> result =
            await _notificationLogService.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
        Assert.Equal(3, result.Count);

        _notificationLogRepositoryMock.Verify(
            repository => repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken),
            Times.Once);

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

        _notificationLogRepositoryMock.Verify(
            repository => repository.GetByDialogId(dialogId, TestContext.Current.CancellationToken),
            Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByShipmentId_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        Guid shipmentId = Guid.NewGuid();
        var expectedException = new InvalidOperationException("Database error");

        _notificationLogRepositoryMock
            .Setup(repository => repository.GetByShipmentId(shipmentId, TestContext.Current.CancellationToken))
            .ThrowsAsync(expectedException);

        // Act
        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _notificationLogService.GetByShipmentId(shipmentId, TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(expectedException, exception);

        _notificationLogRepositoryMock.Verify(
            repository => repository.GetByShipmentId(shipmentId, TestContext.Current.CancellationToken),
            Times.Once);

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

        _notificationLogRepositoryMock.Verify(
            repository => repository.GetByTransmissionId(transmissionId, TestContext.Current.CancellationToken),
            Times.Once);

        _notificationLogRepositoryMock.VerifyNoOtherCalls();
    }

    private static IReadOnlyList<NotificationLogEntry> CreateNotificationLogEntries()
    {
        return
        [
            CreateNotificationLogEntry()
        ];
    }

    private static NotificationLogEntry CreateNotificationLogEntry()
    {
        return new NotificationLogEntry(
            OrderChainId: Guid.NewGuid(),
            ShipmentId: Guid.NewGuid(),
            NotificationId: Guid.NewGuid(),
            CreatorName: "ttd",
            SendersReference: "sender-reference",
            DialogId: Guid.NewGuid().ToString(),
            TransmissionId: Guid.NewGuid().ToString(),
            DeliveryReference: Guid.NewGuid().ToString(),
            Recipient: null,
            Type: "Notification",
            Channel: "Email",
            Destination: "recipient@example.com",
            Resource: "resource",
            Status: "Delivered",
            RequestedSendTime: DateTime.UtcNow,
            LastUpdateTime: DateTime.UtcNow);
    }
}
