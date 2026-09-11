using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.IntegrationTests.Utils;

using Microsoft.Extensions.Options;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

/// <summary>
/// Integration tests verifying that <see cref="OrderRequestService"/>, backed by a real
/// <see cref="IOrderRepository"/> and <see cref="INotificationScheduleService"/>, persists a postponed
/// <see cref="NotificationOrder.RequestedSendTime"/> when an order has a send condition and includes an
/// SMS notification governed by <see cref="SendingTimePolicy.Daytime"/>.
/// </summary>
public sealed class OrderRequestServiceSendConditionPostponementTests : IAsyncLifetime
{
    private readonly List<Guid> _orderIdsToDelete = [];
    private readonly List<Guid> _orderChainIdsToDelete = [];

    public ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_orderIdsToDelete.Count != 0)
        {
            string deleteOrdersSql = $@"DELETE from notifications.orders o where o.alternateid in ('{string.Join("','", _orderIdsToDelete)}')";
            await PostgreUtil.RunSql(deleteOrdersSql);
        }

        if (_orderChainIdsToDelete.Count != 0)
        {
            string deleteChainSql = $@"DELETE from notifications.orderschain oc where oc.orderid in ('{string.Join("','", _orderChainIdsToDelete)}')";
            await PostgreUtil.RunSql(deleteChainSql);
        }
    }

    [Fact]
    public async Task RegisterNotificationOrderChain_SmsWithConditionAndDaytimePolicy_RequestedAfterWindow_PostponesToNextDaytimeWindow()
    {
        // Arrange
        var orderRepository = GetRealOrderRepository();
        var scheduleService = GetRealScheduleService();
        var service = CreateOrderRequestService(orderRepository, scheduleService, GetNoOpContactPointService());

        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        _orderIdsToDelete.Add(orderId);
        _orderChainIdsToDelete.Add(orderChainId);

        // 20:00 UTC is after the Daytime window (09:00-17:00 CET/CEST), so the requested send
        // time should be postponed to the next day's window opening.
        DateTime eveningRequestedSendTime = new DateTime(2026, 6, 15, 20, 0, 0, DateTimeKind.Utc);

        var request = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetOrderChainId(orderChainId)
            .SetCreator(new Creator("ttd"))
            .SetType(OrderType.Notification)
            .SetIdempotencyId($"idempotency-{Guid.NewGuid():N}")
            .SetConditionEndpoint(new Uri("https://vg.no/condition"))
            .SetRequestedSendTime(eveningRequestedSendTime)
            .SetRecipient(new NotificationRecipient
            {
                RecipientSms = new RecipientSms
                {
                    PhoneNumber = "+4799999999",
                    Settings = new SmsSendingOptions
                    {
                        Body = "Test SMS body",
                        SendingTimePolicy = SendingTimePolicy.Daytime
                    }
                }
            })
            .Build();

        // Act
        var result = await service.RegisterNotificationOrderChain(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedOrder = await orderRepository.GetOrderById(result.Value.OrderChainReceipt.ShipmentId, "ttd");
        Assert.NotNull(persistedOrder);

        DateTime expectedPostponedSendTime = scheduleService.GetRequestedSendTimeForDaytimeSendCondition(eveningRequestedSendTime);
        Assert.Equal(expectedPostponedSendTime, persistedOrder.RequestedSendTime);
        Assert.NotEqual(eveningRequestedSendTime, persistedOrder.RequestedSendTime);
    }

    [Fact]
    public async Task RegisterNotificationOrderChain_EmailAndSmsWithConditionAndDaytimePolicy_RequestedAfterWindow_PostponesEmailToo()
    {
        // Arrange
        var orderRepository = GetRealOrderRepository();
        var scheduleService = GetRealScheduleService();
        var service = CreateOrderRequestService(orderRepository, scheduleService, GetContactPointStubForPerson());

        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        _orderIdsToDelete.Add(orderId);
        _orderChainIdsToDelete.Add(orderChainId);

        DateTime eveningRequestedSendTime = new DateTime(2026, 6, 15, 20, 0, 0, DateTimeKind.Utc);

        var request = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetOrderChainId(orderChainId)
            .SetCreator(new Creator("ttd"))
            .SetType(OrderType.Notification)
            .SetIdempotencyId($"idempotency-{Guid.NewGuid():N}")
            .SetConditionEndpoint(new Uri("https://vg.no/condition"))
            .SetRequestedSendTime(eveningRequestedSendTime)
            .SetRecipient(new NotificationRecipient
            {
                RecipientPerson = new RecipientPerson
                {
                    NationalIdentityNumber = "16069412345",
                    ChannelSchema = NotificationChannel.EmailAndSms,
                    SmsSettings = new SmsSendingOptions
                    {
                        Body = "Test SMS body",
                        SendingTimePolicy = SendingTimePolicy.Daytime
                    },
                    EmailSettings = new EmailSendingOptions
                    {
                        Subject = "Test subject",
                        Body = "Test email body"
                    }
                }
            })
            .Build();

        // Act
        var result = await service.RegisterNotificationOrderChain(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedOrder = await orderRepository.GetOrderById(result.Value.OrderChainReceipt.ShipmentId, "ttd");
        Assert.NotNull(persistedOrder);
        Assert.Equal(NotificationChannel.EmailAndSms, persistedOrder.NotificationChannel);

        DateTime expectedPostponedSendTime = scheduleService.GetRequestedSendTimeForDaytimeSendCondition(eveningRequestedSendTime);

        // Because send condition evaluation is order-level, the co-delivered Email notification
        // is postponed together with SMS to keep the single condition check close to send time.
        Assert.Equal(expectedPostponedSendTime, persistedOrder.RequestedSendTime);
        Assert.NotEqual(eveningRequestedSendTime, persistedOrder.RequestedSendTime);
    }

    [Fact]
    public async Task RegisterNotificationOrderChain_SmsWithConditionAndDaytimePolicy_RequestedWithinWindow_IsNotPostponed()
    {
        // Arrange
        var orderRepository = GetRealOrderRepository();
        var scheduleService = GetRealScheduleService();
        var service = CreateOrderRequestService(orderRepository, scheduleService, GetNoOpContactPointService());

        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        _orderIdsToDelete.Add(orderId);
        _orderChainIdsToDelete.Add(orderChainId);

        // 10:00 UTC is within the Daytime window (09:00-17:00 CET/CEST), so no postponement should occur.
        DateTime daytimeRequestedSendTime = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

        var request = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetOrderChainId(orderChainId)
            .SetCreator(new Creator("ttd"))
            .SetType(OrderType.Notification)
            .SetIdempotencyId($"idempotency-{Guid.NewGuid():N}")
            .SetConditionEndpoint(new Uri("https://vg.no/condition"))
            .SetRequestedSendTime(daytimeRequestedSendTime)
            .SetRecipient(new NotificationRecipient
            {
                RecipientSms = new RecipientSms
                {
                    PhoneNumber = "+4799999999",
                    Settings = new SmsSendingOptions
                    {
                        Body = "Test SMS body",
                        SendingTimePolicy = SendingTimePolicy.Daytime
                    }
                }
            })
            .Build();

        // Act
        var result = await service.RegisterNotificationOrderChain(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedOrder = await orderRepository.GetOrderById(result.Value.OrderChainReceipt.ShipmentId, "ttd");
        Assert.NotNull(persistedOrder);
        Assert.Equal(daytimeRequestedSendTime, persistedOrder.RequestedSendTime);
    }

    [Fact]
    public async Task RegisterNotificationOrderChain_SmsWithoutConditionEndpoint_RequestedAfterWindow_IsNotPostponed()
    {
        // Arrange
        var orderRepository = GetRealOrderRepository();
        var scheduleService = GetRealScheduleService();
        var service = CreateOrderRequestService(orderRepository, scheduleService, GetNoOpContactPointService());

        Guid orderId = Guid.NewGuid();
        Guid orderChainId = Guid.NewGuid();
        _orderIdsToDelete.Add(orderId);
        _orderChainIdsToDelete.Add(orderChainId);

        // No ConditionEndpoint set, so postponement should never be applied regardless of time of day.
        DateTime eveningRequestedSendTime = new DateTime(2026, 6, 15, 20, 0, 0, DateTimeKind.Utc);

        var request = new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(orderId)
            .SetOrderChainId(orderChainId)
            .SetCreator(new Creator("ttd"))
            .SetType(OrderType.Notification)
            .SetIdempotencyId($"idempotency-{Guid.NewGuid():N}")
            .SetRequestedSendTime(eveningRequestedSendTime)
            .SetRecipient(new NotificationRecipient
            {
                RecipientSms = new RecipientSms
                {
                    PhoneNumber = "+4799999999",
                    Settings = new SmsSendingOptions
                    {
                        Body = "Test SMS body",
                        SendingTimePolicy = SendingTimePolicy.Daytime
                    }
                }
            })
            .Build();

        // Act
        var result = await service.RegisterNotificationOrderChain(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);

        var persistedOrder = await orderRepository.GetOrderById(result.Value.OrderChainReceipt.ShipmentId, "ttd");
        Assert.NotNull(persistedOrder);
        Assert.Equal(eveningRequestedSendTime, persistedOrder.RequestedSendTime);
    }

    /// <summary>
    /// Resolves the real, database-backed <see cref="IOrderRepository"/> shared across integration tests.
    /// </summary>
    private static IOrderRepository GetRealOrderRepository()
    {
        return (IOrderRepository)ServiceUtil
            .GetServices([typeof(IOrderRepository)])
            .First();
    }

    /// <summary>
    /// Resolves the real <see cref="INotificationScheduleService"/> so the postponement math under test
    /// matches production configuration (send window hours) exactly.
    /// </summary>
    private static INotificationScheduleService GetRealScheduleService()
    {
        return (INotificationScheduleService)ServiceUtil
            .GetServices([typeof(INotificationScheduleService)])
            .First();
    }

    /// <summary>
    /// Constructs the real <see cref="OrderRequestService"/> under test, wired to the real repository and
    /// schedule service, isolating only the external contact-point lookup dependency.
    /// </summary>
    private static OrderRequestService CreateOrderRequestService(
        IOrderRepository orderRepository,
        INotificationScheduleService scheduleService,
        IContactPointService contactPointService)
    {
        var config = Options.Create(new NotificationConfig
        {
            DefaultEmailFromAddress = "noreply@altinn.no",
            DefaultSmsSenderNumber = "Altinn"
        });

        return new OrderRequestService(
            orderRepository,
            contactPointService,
            new GuidService(),
            new DateTimeService(),
            scheduleService,
            config);
    }

    /// <summary>
    /// A contact point service stub that performs no lookups, used for recipients that already carry
    /// their own address information (e.g. <see cref="RecipientSms"/> with an explicit phone number).
    /// </summary>
    private static NoOpContactPointService GetNoOpContactPointService()
    {
        return new NoOpContactPointService();
    }

    /// <summary>
    /// A contact point service stub that adds a fixed email and SMS address for any recipient identified
    /// by national identity number, avoiding a real call to the external KRR/Profile services in tests.
    /// </summary>
    private static StubPersonContactPointService GetContactPointStubForPerson()
    {
        return new StubPersonContactPointService();
    }

    private sealed class NoOpContactPointService : IContactPointService
    {
        public Task AddEmailContactPoints(List<Recipient> recipients, string? resourceId, Altinn.Notifications.Core.Enums.OrderLifecycleStage orderLifecycleStage, bool useStaleContactInfo, string? resourceAction = null) => Task.CompletedTask;

        public Task AddSmsContactPoints(List<Recipient> recipients, string? resourceId, Altinn.Notifications.Core.Enums.OrderLifecycleStage orderLifecycleStage, bool useStaleContactInfo, string? resourceAction = null) => Task.CompletedTask;

        public Task AddEmailAndSmsContactPointsAsync(List<Recipient> recipients, string? resourceId, Altinn.Notifications.Core.Enums.OrderLifecycleStage orderLifecycleStage, bool useStaleContactInfo, string? resourceAction = null) => Task.CompletedTask;

        public Task AddPreferredContactPoints(NotificationChannel channel, List<Recipient> recipients, string? resourceId, Altinn.Notifications.Core.Enums.OrderLifecycleStage orderLifecycleStage, bool useStaleContactInfo, string? resourceAction = null) => Task.CompletedTask;
    }

    private sealed class StubPersonContactPointService : IContactPointService
    {
        public Task AddEmailContactPoints(List<Recipient> recipients, string? resourceId, Altinn.Notifications.Core.Enums.OrderLifecycleStage orderLifecycleStage, bool useStaleContactInfo, string? resourceAction = null)
        {
            foreach (var recipient in recipients)
            {
                recipient.AddressInfo.Add(new Altinn.Notifications.Core.Models.Address.EmailAddressPoint("recipient@example.com"));
            }

            return Task.CompletedTask;
        }

        public Task AddSmsContactPoints(List<Recipient> recipients, string? resourceId, Altinn.Notifications.Core.Enums.OrderLifecycleStage orderLifecycleStage, bool useStaleContactInfo, string? resourceAction = null)
        {
            foreach (var recipient in recipients)
            {
                recipient.AddressInfo.Add(new Altinn.Notifications.Core.Models.Address.SmsAddressPoint("+4799999999"));
            }

            return Task.CompletedTask;
        }

        public Task AddEmailAndSmsContactPointsAsync(List<Recipient> recipients, string? resourceId, Altinn.Notifications.Core.Enums.OrderLifecycleStage orderLifecycleStage, bool useStaleContactInfo, string? resourceAction = null)
        {
            foreach (var recipient in recipients)
            {
                recipient.AddressInfo.Add(new Altinn.Notifications.Core.Models.Address.EmailAddressPoint("recipient@example.com"));
                recipient.AddressInfo.Add(new Altinn.Notifications.Core.Models.Address.SmsAddressPoint("+4799999999"));
            }

            return Task.CompletedTask;
        }

        public Task AddPreferredContactPoints(NotificationChannel channel, List<Recipient> recipients, string? resourceId, Altinn.Notifications.Core.Enums.OrderLifecycleStage orderLifecycleStage, bool useStaleContactInfo, string? resourceAction = null)
        {
            foreach (var recipient in recipients)
            {
                recipient.AddressInfo.Add(new Altinn.Notifications.Core.Models.Address.SmsAddressPoint("+4799999999"));
            }

            return Task.CompletedTask;
        }
    }
}
