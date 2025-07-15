using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
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
    public async Task PersistInstantSmsNotificationAsync_WithValidInstantNotificationOrder_OrdersSuccessfullyPersisted()
    {
        // Arrange
        InstantOrderRepository instantOrderRepository =
            (InstantOrderRepository)ServiceUtil.GetServices([typeof(IInstantOrderRepository)]).First(i => i.GetType() == typeof(InstantOrderRepository));

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

        var smsNotification = new SmsNotification
        {
            OrderId = orderId,
            Id = Guid.NewGuid(),
            Recipient = new SmsRecipient { MobileNumber = "+4799999999" },
            SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
        };

        // Act
        var result = await instantOrderRepository.PersistInstantSmsNotificationAsync(instantNotificationOrder, notificationOrder, smsNotification, DateTime.UtcNow.AddMinutes(15), 1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);

        Assert.NotNull(result.Notification);
        Assert.Equal(orderId, result.Notification.ShipmentId);
        Assert.Equal("DAFA7290-27AE-4958-8CAA-A1F97B6B2307", result.Notification.SendersReference);

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

        string smsNotificationSql = $@"SELECT count(*) FROM notifications.smsnotifications as sn JOIN notifications.orders o ON sn._orderid = o._id WHERE o.alternateid = '{orderId}'";
        int smsNotificationsCount = await PostgreUtil.RunSqlReturnOutput<int>(smsNotificationSql);
        Assert.Equal(1, smsNotificationsCount);
    }

    [Fact]
    public async Task PersistInstantSmsNotificationAsync_InstantNotificationOrderWithDuplicatedIdempotencyId_ThrowsException()
    {
        // Arrange
        InstantOrderRepository instantOrderRepository =
            (InstantOrderRepository)ServiceUtil.GetServices([typeof(IInstantOrderRepository)]).First(i => i.GetType() == typeof(InstantOrderRepository));

        Guid firstOrderId = Guid.NewGuid();
        Guid secondOrderId = Guid.NewGuid();
        Guid firstOrderChainId = Guid.NewGuid();
        Guid secondOrderChainId = Guid.NewGuid();

        string idempotencyId = "DUPLICATE-IDEMPOTENCY-30BC69F09C38";

        _orderIdsToDelete.AddRange([firstOrderId, secondOrderId]);
        _ordersChainIdsToDelete.AddRange([firstOrderChainId, secondOrderChainId]);

        var creationDateTime = DateTime.UtcNow;

        // First instant notification order
        var firstInstantOrder = new InstantNotificationOrder
        {
            Creator = new("ttd"),
            OrderId = firstOrderId,
            Created = creationDateTime,
            IdempotencyId = idempotencyId,
            OrderChainId = firstOrderChainId,
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

        var firstSmsNotification = new SmsNotification
        {
            Id = Guid.NewGuid(),
            OrderId = firstOrderId,
            Recipient = new SmsRecipient { MobileNumber = "+4799999999" },
            SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
        };

        // Save the first order
        await instantOrderRepository.PersistInstantSmsNotificationAsync(firstInstantOrder, firstNotificationOrder, firstSmsNotification, DateTime.UtcNow.AddMinutes(5), 1);

        // Create second order with same idempotency ID
        var secondInstantOrder = new InstantNotificationOrder
        {
            Creator = new("ttd"),
            OrderId = secondOrderId,
            IdempotencyId = idempotencyId,
            OrderChainId = secondOrderChainId,
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

        var secondSmsNotification = new SmsNotification
        {
            Id = Guid.NewGuid(),
            OrderId = secondOrderId,
            Recipient = new SmsRecipient { MobileNumber = "+4799999999" },
            SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await instantOrderRepository.PersistInstantSmsNotificationAsync(secondInstantOrder, secondNotificationOrder, secondSmsNotification, DateTime.UtcNow.AddMinutes(10), 1));

        // Verify Orders Chain
        string persistedOrderChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{firstOrderChainId}'";
        int persistedOrderChainCount = await PostgreUtil.RunSqlReturnOutput<int>(persistedOrderChainSql);
        Assert.Equal(1, persistedOrderChainCount);

        string notPersistedOrderChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{secondOrderChainId}'";
        int notPersistedOrderChainCount = await PostgreUtil.RunSqlReturnOutput<int>(notPersistedOrderChainSql);
        Assert.Equal(0, notPersistedOrderChainCount);

        // Verify Orders
        string persistedOrderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{firstOrderId}' and type = 'Instant'";
        int persistedOrderCount = await PostgreUtil.RunSqlReturnOutput<int>(persistedOrderSql);
        Assert.Equal(1, persistedOrderCount);

        string notPersistedOrderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{secondOrderId}' and type = 'Instant'";
        int notPersistedOrderCount = await PostgreUtil.RunSqlReturnOutput<int>(notPersistedOrderSql);
        Assert.Equal(0, notPersistedOrderCount);

        // Verify SMS texts
        string persistedSmsTextSql = $@"SELECT count(*) FROM notifications.smstexts AS st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{firstOrderId}'";
        int persistedSmsTextCount = await PostgreUtil.RunSqlReturnOutput<int>(persistedSmsTextSql);
        Assert.Equal(1, persistedSmsTextCount);

        string notPersistedSmsTextSql = $@"SELECT count(*) FROM notifications.smstexts AS st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{secondOrderId}'";
        int notPersistedSmsTextCount = await PostgreUtil.RunSqlReturnOutput<int>(notPersistedSmsTextSql);
        Assert.Equal(0, notPersistedSmsTextCount);

        // Verify SMS notifications
        string persistedSmsNotificationSql = $@"SELECT count(*) FROM notifications.smsnotifications AS sn JOIN notifications.orders o ON sn._orderid = o._id WHERE o.alternateid = '{firstOrderId}'";
        int persistedSmsNotificationCount = await PostgreUtil.RunSqlReturnOutput<int>(persistedSmsNotificationSql);
        Assert.Equal(1, persistedSmsNotificationCount);

        string notPersistedSmsNotificationSql = $@"SELECT count(*) FROM notifications.smsnotifications AS sn JOIN notifications.orders o ON sn._orderid = o._id WHERE o.alternateid = '{secondOrderId}'";
        int notPersistedSmsNotificationCount = await PostgreUtil.RunSqlReturnOutput<int>(notPersistedSmsNotificationSql);
        Assert.Equal(0, notPersistedSmsNotificationCount);
    }

    [Fact]
    public async Task PersistInstantSmsNotificationAsync_InstantNotificationOrderWithCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        InstantOrderRepository instantOrderRepository =
            (InstantOrderRepository)ServiceUtil.GetServices([typeof(IInstantOrderRepository)]).First(i => i.GetType() == typeof(InstantOrderRepository));

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

        var smsNotification = new SmsNotification
        {
            OrderId = orderId,
            Id = Guid.NewGuid(),
            Recipient = new SmsRecipient { MobileNumber = "+4799999999" },
            SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
        };

        // Create a cancellation token that's already cancelled
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await instantOrderRepository.PersistInstantSmsNotificationAsync(instantNotificationOrder, notificationOrder, smsNotification, DateTime.UtcNow.AddMinutes(25), 1, cancellationTokenSource.Token));

        // Verify nothing was persisted
        string orderChainSql = $@"SELECT count(*) FROM notifications.orderschain WHERE orderid = '{orderChainId}'";
        int orderChainCount = await PostgreUtil.RunSqlReturnOutput<int>(orderChainSql);
        Assert.Equal(0, orderChainCount);

        string orderSql = $@"SELECT count(*) FROM notifications.orders WHERE alternateid = '{orderId}'";
        int orderCount = await PostgreUtil.RunSqlReturnOutput<int>(orderSql);
        Assert.Equal(0, orderCount);

        string smsTextSql = $@"SELECT count(*) FROM notifications.smstexts AS st JOIN notifications.orders o ON st._orderid = o._id WHERE o.alternateid = '{orderId}'";
        int smsTextCount = await PostgreUtil.RunSqlReturnOutput<int>(smsTextSql);
        Assert.Equal(0, smsTextCount);

        string smsNotificationSql = $@"SELECT count(*) FROM notifications.smsnotifications AS sn JOIN notifications.orders o ON sn._orderid = o._id WHERE o.alternateid = '{orderId}'";
        int smsNotificationCount = await PostgreUtil.RunSqlReturnOutput<int>(smsNotificationSql);
        Assert.Equal(0, smsNotificationCount);
    }

    [Fact]
    public async Task RetrieveTrackingInformation_WhenNonExistentCreatorAndIdempotencyId_ReturnsNull()
    {
        // Arrange
        InstantOrderRepository instantOrderRepository =
            (InstantOrderRepository)ServiceUtil.GetServices([typeof(IInstantOrderRepository)]).First(i => i.GetType() == typeof(InstantOrderRepository));

        string creatorName = "ttd";
        string idempotencyId = "1091990A-D05D-4326-A1D7-60420F4E8B1E";

        // Act
        var result = await instantOrderRepository.RetrieveTrackingInformation(creatorName, idempotencyId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RetrieveTrackingInformation_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        InstantOrderRepository instantOrderRepository =
            (InstantOrderRepository)ServiceUtil.GetServices([typeof(IInstantOrderRepository)]).First(i => i.GetType() == typeof(InstantOrderRepository));

        string creatorName = "ttd";
        string idempotencyId = "2C2024D9-0A82-4BA5-A71F-17D33D0EFEC9";

        // Create a cancellation token that's already cancelled
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await instantOrderRepository.RetrieveTrackingInformation(creatorName, idempotencyId, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task RetrieveTrackingInformation_WithValidCreatorAndIdempotencyId_ReturnsExpectedOrderDetails()
    {
        // Arrange
        InstantOrderRepository instantOrderRepository =
            (InstantOrderRepository)ServiceUtil.GetServices([typeof(IInstantOrderRepository)]).First(i => i.GetType() == typeof(InstantOrderRepository));

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

        var smsNotification = new SmsNotification
        {
            OrderId = orderId,
            Id = Guid.NewGuid(),
            Recipient = new SmsRecipient { MobileNumber = "+4799999999" },
            SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
        };

        // Create the order in the database
        await instantOrderRepository.PersistInstantSmsNotificationAsync(instantNotificationOrder, notificationOrder, smsNotification, DateTime.UtcNow.AddMinutes(20), 1);

        // Act
        var result = await instantOrderRepository.RetrieveTrackingInformation(creator, idempotencyId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderChainId, result.OrderChainId);
        Assert.Equal(orderId, result.Notification.ShipmentId);
        Assert.Equal(sendersReference, result.Notification.SendersReference);
    }

    [Fact]
    public async Task RetrieveTrackingInformation_RequiresMatchingCreatorAndIdempotencyId_ReturnsNullWhenCreatorMismatches()
    {
        // Arrange
        InstantOrderRepository instantOrderRepository =
            (InstantOrderRepository)ServiceUtil.GetServices([typeof(IInstantOrderRepository)]).First(i => i.GetType() == typeof(InstantOrderRepository));

        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();

        var creationDateTime = DateTime.UtcNow;

        string creator = "ttd";
        string invalidCreator = "not-ttd";
        string idempotencyId = "08556351-748F-4B05-A42A-1BA91DD5C275";
        string senderReference = "F15C804E-9C66-4968-916F-9E71C4C6FB63";

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

        var smsNotification = new SmsNotification
        {
            OrderId = orderId,
            Id = Guid.NewGuid(),
            Recipient = new SmsRecipient { MobileNumber = "+4799999999" },
            SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
        };

        // Create the order in the database
        await instantOrderRepository.PersistInstantSmsNotificationAsync(instantNotificationOrder, notificationOrder, smsNotification, DateTime.UtcNow.AddMinutes(15), 1);

        // Act
        var result = await instantOrderRepository.RetrieveTrackingInformation(invalidCreator, idempotencyId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RetrieveTrackingInformation_RequiresMatchingCreatorAndIdempotencyId_ReturnsNullWhenIdempotencyIdMismatches()
    {
        // Arrange
        InstantOrderRepository instantOrderRepository =
            (InstantOrderRepository)ServiceUtil.GetServices([typeof(IInstantOrderRepository)]).First(i => i.GetType() == typeof(InstantOrderRepository));

        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();

        var creationDateTime = DateTime.UtcNow;

        string creator = "ttd";
        string idempotencyId = "7D4DF1D4-4E55-4BDC-ACA1-0331D47AC28F";
        string senderReference = "CB11C461-8887-4887-9A0B-3878F99D13F6";
        string invalidIdempotencyId = "720D256D-0A2A-4C5F-BD9A-A32634271CD2";

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
                        Message = "Test message for wrong idempotency identifier"
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

        var smsNotification = new SmsNotification
        {
            OrderId = orderId,
            Id = Guid.NewGuid(),
            Recipient = new SmsRecipient { MobileNumber = "+4799999999" },
            SendResult = new(SmsNotificationResultType.New, DateTime.UtcNow)
        };

        // Create the order in the database
        await instantOrderRepository.PersistInstantSmsNotificationAsync(instantNotificationOrder, notificationOrder, smsNotification, DateTime.UtcNow.AddMinutes(10), 1);

        // Act
        var result = await instantOrderRepository.RetrieveTrackingInformation(creator, invalidIdempotencyId);

        // Assert
        Assert.Null(result);
    }
}
