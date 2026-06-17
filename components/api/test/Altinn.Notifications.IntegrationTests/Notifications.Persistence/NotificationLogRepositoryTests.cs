using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public sealed class NotificationLogRepositoryTests : IAsyncLifetime
{
    private readonly List<Guid> _orderIdsToCleanup = [];
    private readonly List<Guid> _orderChainIdsToCleanup = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (Guid orderId in _orderIdsToCleanup)
        {
            await PostgreUtil.DeleteNotificationLogFromDb(orderId);
            await PostgreUtil.DeleteOrderFromDb(orderId);
        }

        await PostgreUtil.DeleteOrdersChainByOrderIds(_orderChainIdsToCleanup);
    }

    [Fact]
    public async Task InsertAsync_WithChainedEmailOrder_PersistsAllNotificationLogProperties()
    {
        // Arrange
        var dialogId = Guid.NewGuid();
        const string transmissionId = "test-transmission-abc123";
        const string toAddress = "log-test@example.com";
        const string operationId = "op-id-test-abc123";
        const string resourceId = "ttd-resource";
        const string creatorName = "ttd";

        (Guid orderId, long chainDbId, Guid orderChainId) = await PostgreUtil.PopulateDBWithChainedOrderAndEmailNotification(
            dialogId: dialogId,
            transmissionId: transmissionId,
            toAddress: toAddress,
            operationId: operationId,
            creatorName: creatorName,
            resourceId: resourceId);

        _orderIdsToCleanup.Add(orderId);
        _orderChainIdsToCleanup.Add(orderChainId);

        NotificationLogRepository repo = (NotificationLogRepository)ServiceUtil
           .GetServices([typeof(INotificationLogRepository)])
           .First(i => i.GetType() == typeof(NotificationLogRepository));

        // Act
        int rowsInserted = await repo.InsertAsync(orderId);

        // Assert — row count
        Assert.Equal(1, rowsInserted);

        // Assert — persisted properties
        NotificationLogEntry? entry = await PostgreUtil.GetNotificationLogEntry(orderId);
        Assert.NotNull(entry);

        Assert.Equal(orderId, entry.ShipmentId);
        Assert.Equal(chainDbId, entry.OrderChainId);
        Assert.Equal(creatorName, entry.CreatorName);
        Assert.Equal(dialogId, entry.DialogId);
        Assert.Equal(transmissionId, entry.TransmissionId);
        Assert.Equal(toAddress, entry.Destination);
        Assert.Equal(resourceId, entry.Resource);
        Assert.Equal("Notification", entry.Type);
        Assert.Equal("Delivered", entry.Status);
        Assert.Equal(operationId, entry.OperationId);  // set via updateemailnotification_v3
        Assert.Null(entry.Recipient);
        Assert.Null(entry.GatewayReference);
    }

    [Fact]
    public async Task InsertAsync_WithChainedSmsOrder_PersistsAllNotificationLogProperties()
    {
        // Arrange
        var dialogId = Guid.NewGuid();
        const string transmissionId = "test-transmission-sms-456";
        const string mobileNumber = "+4799999999";
        const string gatewayReference = "gw-ref-test-123";
        const string resourceId = "ttd-resource";
        const string creatorName = "ttd";

        (Guid orderId, long chainDbId, Guid orderChainId) = await PostgreUtil.PopulateDBWithChainedOrderAndSmsNotification(
            dialogId: dialogId,
            transmissionId: transmissionId,
            mobileNumber: mobileNumber,
            gatewayReference: gatewayReference,
            creatorName: creatorName,
            resourceId: resourceId);

        _orderIdsToCleanup.Add(orderId);
        _orderChainIdsToCleanup.Add(orderChainId);

        NotificationLogRepository repo = (NotificationLogRepository)ServiceUtil
            .GetServices([typeof(INotificationLogRepository)])
            .First(i => i.GetType() == typeof(NotificationLogRepository));

        // Act
        int rowsInserted = await repo.InsertAsync(orderId);

        // Assert — row count
        Assert.Equal(1, rowsInserted);

        // Assert — persisted properties
        NotificationLogEntry? entry = await PostgreUtil.GetNotificationLogEntry(orderId);
        Assert.NotNull(entry);

        Assert.Equal(orderId, entry.ShipmentId);
        Assert.Equal(chainDbId, entry.OrderChainId);
        Assert.Equal(creatorName, entry.CreatorName);
        Assert.Equal(dialogId, entry.DialogId);
        Assert.Equal(transmissionId, entry.TransmissionId);
        Assert.Equal(mobileNumber, entry.Destination);
        Assert.Equal(resourceId, entry.Resource);
        Assert.Equal("Notification", entry.Type);
        Assert.Equal("Delivered", entry.Status);
        Assert.Null(entry.Recipient);
        Assert.Equal(gatewayReference, entry.GatewayReference);  // SMS path — gateway reference is set
        Assert.Null(entry.OperationId);                          // SMS path — operation ID is null
    }

    [Fact]
    public async Task InsertAsync_WithStandaloneOrder_ChainDerivedFieldsAreNull()
    {
        // Arrange — standalone order has no orderschain row, so _orderchainid is NULL.
        (NotificationOrder order, EmailNotification _) =
            await PostgreUtil.PopulateDBWithOrderAndEmailNotification(simulateCronJob: true, simulateConsumers: true);

        _orderIdsToCleanup.Add(order.Id);

        NotificationLogRepository repo = (NotificationLogRepository)ServiceUtil
            .GetServices([typeof(INotificationLogRepository)])
            .First(i => i.GetType() == typeof(NotificationLogRepository));

        // Act
        int rowsInserted = await repo.InsertAsync(order.Id);

        // Assert — one log row was created
        Assert.Equal(1, rowsInserted);

        // Assert — fields sourced from orderschain must all be null for a standalone order
        NotificationLogEntry? entry = await PostgreUtil.GetNotificationLogEntry(order.Id);
        Assert.NotNull(entry);

        Assert.Equal(order.Id, entry.ShipmentId);
        Assert.Null(entry.OrderChainId);
        Assert.Null(entry.DialogId);
        Assert.Null(entry.TransmissionId);
    }
}
