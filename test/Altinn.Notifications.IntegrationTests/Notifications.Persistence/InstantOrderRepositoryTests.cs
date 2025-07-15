using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public class InstantOrderRepositoryTests : IAsyncLifetime
{
    private readonly List<Guid> _orderIdsToDelete;
    private readonly List<Guid> _ordersChainIdsToDelete;

    public InstantOrderRepositoryTests()
    {
        _orderIdsToDelete = [];
        _ordersChainIdsToDelete = [];
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_orderIdsToDelete.Count != 0)
        {
            string deleteSql = $@"DELETE from notifications.orders o where o.alternateid in ('{string.Join("','", _orderIdsToDelete)}')";
            await PostgreUtil.RunSql(deleteSql);
        }

        if (_ordersChainIdsToDelete.Count != 0)
        {
            string deleteSql = $@"DELETE from notifications.orderschain oc where oc.orderid in ('{string.Join("','", _ordersChainIdsToDelete)}')";
            await PostgreUtil.RunSql(deleteSql);
        }
    }

    [Fact]
    public async Task Create_InstantNotificationOrder_SuccessfullyPersistedInDatabase()
    {
        // Arrange
        InstantOrderRepository repo = (InstantOrderRepository)ServiceUtil.GetServices([typeof(IInstantOrderRepository)]).First(i => i.GetType() == typeof(InstantOrderRepository));

        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();

        _orderIdsToDelete.Add(orderId);
        _ordersChainIdsToDelete.Add(orderChainId);

        var creationDateTime = DateTime.UtcNow;

        var instantNotificationOrder = new InstantNotificationOrder
        {
            OrderId = orderId,
            Creator = new("ttd"),
            Created = creationDateTime,
            OrderChainId = orderChainId,
            IdempotencyId = "F6E76FA5-0A53-4195-A702-21ECCC77B9E8",
            SendersReference = "DAFA7290-27AE-4958-8CAA-A1F97B6B2307",

            InstantNotificationRecipient = new InstantNotificationRecipient
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                {
                    PhoneNumber = "+4799999999",
                    TimeToLiveInSeconds = 3600,
                    ShortMessageContent = new ShortMessageContent
                    {
                        Sender = "Altinn",
                        Message = "This is an urgent test message"
                    }
                }
            }
        };

        NotificationOrder notificationOrder = new()
        {
            Id = orderId,
            Creator = new("ttd"),
            Type = OrderType.Instant,
            Created = creationDateTime,
            RequestedSendTime = creationDateTime,
            NotificationChannel = NotificationChannel.Sms,
            SendersReference = "DAFA7290-27AE-4958-8CAA-A1F97B6B2307",
            Templates =
            [
                new SmsTemplate("Altinn", "This is an urgent test message")
            ],
            Recipients =
            [
                new Recipient([new SmsAddressPoint("+4799999999")])
            ]
        };

        // Act
        var result = await repo.Create(instantNotificationOrder, notificationOrder);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderId, result.OrderId);
        Assert.Equal(OrderType.Instant, result.Type);
        Assert.Equal("ttd", result.Creator.ShortName);
        Assert.Equal(orderChainId, result.OrderChainId);
        Assert.Equal("F6E76FA5-0A53-4195-A702-21ECCC77B9E8", result.IdempotencyId);
        Assert.Equal("DAFA7290-27AE-4958-8CAA-A1F97B6B2307", result.SendersReference);

        // Verify database persistence
        string orderChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{orderChainId}'";
        int orderChainCount = await PostgreUtil.RunSqlReturnOutput<int>(orderChainSql);
        Assert.Equal(1, orderChainCount);

        string orderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{orderId}' and type = 'Instant'";
        int orderCount = await PostgreUtil.RunSqlReturnOutput<int>(orderSql);
        Assert.Equal(1, orderCount);

        string smsTextSql = $@"SELECT count(*) FROM notifications.smstexts as st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{orderId}'";
        int smsTextCount = await PostgreUtil.RunSqlReturnOutput<int>(smsTextSql);
        Assert.Equal(1, smsTextCount);
    }

    [Fact]
    public async Task Create_InstantNotificationOrder_WithCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        InstantOrderRepository repo = (InstantOrderRepository)ServiceUtil.GetServices([typeof(IInstantOrderRepository)]).First(i => i.GetType() == typeof(InstantOrderRepository));

        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();

        _orderIdsToDelete.Add(orderId);
        _ordersChainIdsToDelete.Add(orderChainId);

        var creationDateTime = DateTime.UtcNow;

        var instantNotificationOrder = new InstantNotificationOrder
        {
            OrderId = orderId,
            Creator = new("ttd"),
            Created = creationDateTime,
            OrderChainId = orderChainId,
            IdempotencyId = "INSTANT-CANCEL-1E3CD83E99FD",
            SendersReference = "INSTANT-CANCEL-4B8FE77B9455",
            InstantNotificationRecipient = new InstantNotificationRecipient
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                {
                    PhoneNumber = "+4799999999",
                    TimeToLiveInSeconds = 9000,
                    ShortMessageContent = new ShortMessageContent
                    {
                        Sender = "Altinn",
                        Message = "This message should not be persisted"
                    }
                }
            }
        };

        NotificationOrder notificationOrder = new()
        {
            Id = orderId,
            Creator = new("ttd"),
            Type = OrderType.Instant,
            Created = creationDateTime,
            RequestedSendTime = creationDateTime,
            NotificationChannel = NotificationChannel.Sms,
            SendersReference = "INSTANT-CANCEL-4B8FE77B9455",
            Templates =
            [
                new SmsTemplate("Altinn", "This message should not be persisted")
            ],
            Recipients =
            [
                new Recipient([new SmsAddressPoint("+4799999999")])
            ]
        };

        // Create a cancellation token that's already cancelled
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await repo.Create(instantNotificationOrder, notificationOrder, cancellationTokenSource.Token));

        // Verify nothing was persisted
        string orderChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{orderChainId}'";
        int orderChainCount = await PostgreUtil.RunSqlReturnOutput<int>(orderChainSql);
        Assert.Equal(0, orderChainCount);

        string orderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{orderId}'";
        int orderCount = await PostgreUtil.RunSqlReturnOutput<int>(orderSql);
        Assert.Equal(0, orderCount);
    }

    [Fact]
    public async Task Create_InstantNotificationOrder_WithDuplicateIdempotencyId_ThrowsException()
    {
        // Arrange
        InstantOrderRepository repo = (InstantOrderRepository)ServiceUtil.GetServices([typeof(IInstantOrderRepository)]).First(i => i.GetType() == typeof(InstantOrderRepository));

        Guid orderChainId = Guid.NewGuid();
        Guid firstOrderId = Guid.NewGuid();
        Guid secondOrderId = Guid.NewGuid();
        string idempotencyId = "DUPLICATE-IDEMPOTENCY-30BC69F09C38";

        _orderIdsToDelete.Add(firstOrderId);
        _orderIdsToDelete.Add(secondOrderId);
        _ordersChainIdsToDelete.Add(orderChainId);

        var creationDateTime = DateTime.UtcNow;

        // First instant notification order
        var firstInstantOrder = new InstantNotificationOrder
        {
            Creator = new("ttd"),
            OrderId = firstOrderId,
            Created = creationDateTime,
            OrderChainId = orderChainId,
            IdempotencyId = idempotencyId,
            SendersReference = "F4B120EF-7DBD-438A-8402-02D21833602B",

            InstantNotificationRecipient = new InstantNotificationRecipient
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                {
                    PhoneNumber = "+4799999999",
                    TimeToLiveInSeconds = 7200,
                    ShortMessageContent = new ShortMessageContent
                    {
                        Sender = "Altinn",
                        Message = "First message with duplicate idempotency ID"
                    }
                }
            }
        };

        NotificationOrder firstNotificationOrder = new()
        {
            Id = firstOrderId,
            Creator = new("ttd"),
            Type = OrderType.Instant,
            Created = creationDateTime,
            RequestedSendTime = creationDateTime,
            NotificationChannel = NotificationChannel.Sms,
            SendersReference = "F4B120EF-7DBD-438A-8402-02D21833602B",
            Templates =
            [
                new SmsTemplate("Altinn", "First message with duplicate idempotency ID")
            ],
            Recipients =
            [
                new Recipient([new SmsAddressPoint("+4799999999")])
            ]
        };

        // Save the first order
        await repo.Create(firstInstantOrder, firstNotificationOrder);

        // Create second order with same idempotency ID
        var secondInstantOrder = new InstantNotificationOrder
        {
            Creator = new("ttd"),
            OrderId = secondOrderId,
            IdempotencyId = idempotencyId,
            OrderChainId = Guid.NewGuid(),
            Created = creationDateTime.AddMinutes(5),
            SendersReference = "C075F863-3E89-4688-9B31-D8817FECDF6B",
            InstantNotificationRecipient = new InstantNotificationRecipient
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                {
                    PhoneNumber = "+4788888888",
                    TimeToLiveInSeconds = 7260,
                    ShortMessageContent = new ShortMessageContent
                    {
                        Sender = "Altinn",
                        Message = "Second message with duplicate idempotency ID"
                    }
                }
            }
        };

        NotificationOrder secondNotificationOrder = new()
        {
            Id = secondOrderId,
            Creator = new("ttd"),
            Type = OrderType.Instant,
            Created = creationDateTime.AddMinutes(5),
            NotificationChannel = NotificationChannel.Sms,
            RequestedSendTime = creationDateTime.AddMinutes(5),
            SendersReference = "C075F863-3E89-4688-9B31-D8817FECDF6B",
            Templates =
            [
                new SmsTemplate("Altinn", "Second message with duplicate idempotency ID")
            ],
            Recipients =
            [
                new Recipient([new SmsAddressPoint("+4788888888")])
            ]
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await repo.Create(secondInstantOrder, secondNotificationOrder));

        // Verify only the first order was persisted
        string orderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid IN ('{firstOrderId}', '{secondOrderId}')";
        int orderCount = await PostgreUtil.RunSqlReturnOutput<int>(orderSql);
        Assert.Equal(1, orderCount);
    }

    [Fact]
    public async Task GetInstantOrderTracking_WhenNonExistentCreatorAndIdempotencyId_ReturnsNull()
    {
        // Arrange
        InstantOrderRepository repo = (InstantOrderRepository)ServiceUtil.GetServices([typeof(IInstantOrderRepository)]).First(i => i.GetType() == typeof(InstantOrderRepository));

        string creatorName = "ttd";
        string idempotencyId = "1091990A-D05D-4326-A1D7-60420F4E8B1E";

        // Act
        var result = await repo.GetInstantOrderTracking(creatorName, idempotencyId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetInstantOrderTracking_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        InstantOrderRepository repo = (InstantOrderRepository)ServiceUtil.GetServices([typeof(IInstantOrderRepository)]).First(i => i.GetType() == typeof(InstantOrderRepository));

        string creatorName = "ttd";
        string idempotencyId = "2C2024D9-0A82-4BA5-A71F-17D33D0EFEC9";

        // Create a cancellation token that's already cancelled
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await repo.GetInstantOrderTracking(creatorName, idempotencyId, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task GetInstantOrderTracking_WithValidCreatorAndIdempotencyId_ReturnsExpectedOrderDetails()
    {
        // Arrange
        InstantOrderRepository repo = (InstantOrderRepository)ServiceUtil.GetServices([typeof(IInstantOrderRepository)]).First(i => i.GetType() == typeof(InstantOrderRepository));

        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();

        var creationDateTime = DateTime.UtcNow;

        string creator = "ttd";
        string idempotencyId = "9EFB6947-BBB1-4DF2-9466-CE44CD1A46B0";
        string sendersReference = "BB69F687-AF95-4790-AB27-DED218B4800B";

        _orderIdsToDelete.Add(orderId);
        _ordersChainIdsToDelete.Add(orderChainId);

        var instantNotificationOrder = new InstantNotificationOrder
        {
            OrderId = orderId,
            Creator = new(creator),
            Created = creationDateTime,
            OrderChainId = orderChainId,
            IdempotencyId = idempotencyId,
            SendersReference = sendersReference,
            InstantNotificationRecipient = new InstantNotificationRecipient
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                {
                    PhoneNumber = "+4799999999",
                    TimeToLiveInSeconds = 3600,
                    ShortMessageContent = new ShortMessageContent
                    {
                        Sender = "Altinn",
                        Message = "Test message for tracking"
                    }
                }
            }
        };

        NotificationOrder notificationOrder = new()
        {
            Id = orderId,
            Creator = new(creator),
            Type = OrderType.Instant,
            Created = creationDateTime,
            SendersReference = sendersReference,
            RequestedSendTime = creationDateTime,
            NotificationChannel = NotificationChannel.Sms,
            Templates =
            [
                new SmsTemplate("Altinn", "Test message for tracking")
            ],
            Recipients =
            [
                new Recipient([new SmsAddressPoint("+4799999999")])
            ]
        };

        // Create the order in the database
        await repo.Create(instantNotificationOrder, notificationOrder);

        // Act
        var result = await repo.GetInstantOrderTracking(creator, idempotencyId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);
        Assert.Equal(orderId, result.Notification.ShipmentId);
        Assert.Equal(sendersReference, result.Notification.SendersReference);
    }

    [Fact]
    public async Task GetInstantOrderTracking_RequiresMatchingCreatorAndIdempotencyId_ReturnsNullWhenCreatorMismatches()
    {
        // Arrange
        InstantOrderRepository repo = (InstantOrderRepository)ServiceUtil.GetServices([typeof(IInstantOrderRepository)]).First(i => i.GetType() == typeof(InstantOrderRepository));

        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();

        string creator = "ttd";
        string wrongCreator = "not-ttd";
        string idempotencyId = "08556351-748F-4B05-A42A-1BA91DD5C275";
        string senderReference = "F15C804E-9C66-4968-916F-9E71C4C6FB63";

        var creationDateTime = DateTime.UtcNow;

        _orderIdsToDelete.Add(orderId);
        _ordersChainIdsToDelete.Add(orderChainId);

        var instantNotificationOrder = new InstantNotificationOrder
        {
            OrderId = orderId,
            Creator = new(creator),
            Created = creationDateTime,
            OrderChainId = orderChainId,
            IdempotencyId = idempotencyId,
            SendersReference = senderReference,
            InstantNotificationRecipient = new InstantNotificationRecipient
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                {
                    PhoneNumber = "+4799999999",
                    TimeToLiveInSeconds = 3600,
                    ShortMessageContent = new ShortMessageContent
                    {
                        Sender = "Altinn",
                        Message = "Test message for wrong creator"
                    }
                }
            }
        };

        NotificationOrder notificationOrder = new()
        {
            Id = orderId,
            Creator = new(creator),
            Type = OrderType.Instant,
            Created = creationDateTime,
            SendersReference = senderReference,
            RequestedSendTime = creationDateTime,
            NotificationChannel = NotificationChannel.Sms,
            Templates =
            [
                new SmsTemplate("Altinn", "Test message for wrong creator")
            ],
            Recipients =
            [
                new Recipient([new SmsAddressPoint("+4799999999")])
            ]
        };

        // Create the order in the database
        await repo.Create(instantNotificationOrder, notificationOrder);

        // Act
        var result = await repo.GetInstantOrderTracking(wrongCreator, idempotencyId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetInstantOrderTracking_RequiresMatchingCreatorAndIdempotencyId_ReturnsNullWhenIdempotencyIdMismatches()
    {
        // Arrange
        InstantOrderRepository repo = (InstantOrderRepository)ServiceUtil.GetServices([typeof(IInstantOrderRepository)]).First(i => i.GetType() == typeof(InstantOrderRepository));

        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();

        string creator = "ttd";
        string idempotencyId = "7D4DF1D4-4E55-4BDC-ACA1-0331D47AC28F";
        string senderReference = "CB11C461-8887-4887-9A0B-3878F99D13F6";
        string wrongIdempotencyId = "720D256D-0A2A-4C5F-BD9A-A32634271CD2";

        var creationDateTime = DateTime.UtcNow;

        _orderIdsToDelete.Add(orderId);
        _ordersChainIdsToDelete.Add(orderChainId);

        var instantNotificationOrder = new InstantNotificationOrder
        {
            OrderId = orderId,
            Creator = new(creator),
            Created = creationDateTime,
            OrderChainId = orderChainId,
            IdempotencyId = idempotencyId,
            SendersReference = senderReference,
            InstantNotificationRecipient = new InstantNotificationRecipient
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                {
                    PhoneNumber = "+4799999999",
                    TimeToLiveInSeconds = 3600,
                    ShortMessageContent = new ShortMessageContent
                    {
                        Sender = "Altinn",
                        Message = "Test message for wrong idempotency ID"
                    }
                }
            }
        };

        NotificationOrder notificationOrder = new()
        {
            Id = orderId,
            Creator = new(creator),
            Type = OrderType.Instant,
            Created = creationDateTime,
            SendersReference = senderReference,
            RequestedSendTime = creationDateTime,
            NotificationChannel = NotificationChannel.Sms,
            Templates =
            [
                new SmsTemplate("Altinn", "Test message for wrong idempotency ID")
            ],
            Recipients =
            [
                new Recipient([new SmsAddressPoint("+4799999999")])
            ]
        };

        // Create the order in the database
        await repo.Create(instantNotificationOrder, notificationOrder);

        // Act
        var result = await repo.GetInstantOrderTracking(creator, wrongIdempotencyId);

        // Assert
        Assert.Null(result);
    }
}
