using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.IntegrationTests.Utils;
using Altinn.Notifications.Persistence.Repository;

using Xunit;

namespace Altinn.Notifications.IntegrationTests.Notifications.Persistence;

public sealed class DashboardRepositoryTests : IAsyncLifetime
{
    private readonly List<Guid> _orderIdsToDelete = [];
    private readonly string _recipientNin = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_orderIdsToDelete.Count > 0)
        {
            await PostgreUtil.DeleteOrdersByAlternateIds(_orderIdsToDelete);
        }
    }

    [Fact]
    public async Task GetDashboardNotificationsByNinAsync_ReturnsEmailAndSmsForRecipientWithinRange()
    {
        // Arrange
        await SeedOrderWithEmailNotification(_recipientNin, requestedSendTime: new DateTime(2023, 06, 16, 08, 50, 00, DateTimeKind.Utc));
        await SeedOrderWithSmsNotification(_recipientNin, requestedSendTime: new DateTime(2023, 06, 16, 09, 00, 00, DateTimeKind.Utc));

        DashboardRepository sut = GetRepository();

        // Act
        var result = await sut.GetDashboardNotificationsByNinAsync(
            _recipientNin,
            new DateTime(2023, 06, 01, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2023, 07, 01, 0, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, n => n.DeliveryAttempts.Any(r => r.Channel == "email" && r.NationalIdentityNumber == _recipientNin));
        Assert.Contains(result, n => n.DeliveryAttempts.Any(r => r.Channel == "sms" && r.NationalIdentityNumber == _recipientNin));
    }

    [Fact]
    public async Task GetDashboardNotificationsByNinAsync_UnknownNin_ReturnsEmpty()
    {
        // Arrange — seed a notification for our NIN so the table is non-empty
        await SeedOrderWithEmailNotification(_recipientNin, requestedSendTime: new DateTime(2023, 06, 16, 08, 50, 00, DateTimeKind.Utc));

        DashboardRepository sut = GetRepository();

        // Act — query with a different NIN
        var result = await sut.GetDashboardNotificationsByNinAsync(
            "00000000000",
            new DateTime(2023, 06, 01, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2023, 07, 01, 0, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDashboardNotificationsByNinAsync_DateRangeExcludesNotifications_ReturnsEmpty()
    {
        // Arrange
        await SeedOrderWithEmailNotification(_recipientNin, requestedSendTime: new DateTime(2023, 06, 16, 08, 50, 00, DateTimeKind.Utc));

        DashboardRepository sut = GetRepository();

        // Act — date window after the requestedsendtime
        var result = await sut.GetDashboardNotificationsByNinAsync(
            _recipientNin,
            new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 02, 01, 0, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDashboardNotificationsByNinAsync_NullDates_DefaultsToLastSevenDays()
    {
        // Arrange — order inside the default 7-day window, plus one outside it
        await SeedOrderWithEmailNotification(_recipientNin, requestedSendTime: DateTime.UtcNow.AddDays(-1));
        await SeedOrderWithEmailNotification(_recipientNin, requestedSendTime: DateTime.UtcNow.AddDays(-30));

        DashboardRepository sut = GetRepository();

        // Act
        var result = await sut.GetDashboardNotificationsByNinAsync(_recipientNin, null, null, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.All(result, n => Assert.All(n.DeliveryAttempts, r => Assert.Equal("email", r.Channel)));
    }

    [Fact]
    public async Task GetDashboardNotificationsByNinAsync_SingleOrderWithEmailAndSms_GroupedUnderOneShipment()
    {
        // Arrange — one order that produced both an email and an SMS notification for the same NIN.
        // The SQL function returns two rows with the same shipmentid, so the second row must hit
        // the groups.TryGetValue(shipmentId, ...) == true branch and be appended to the same entry.
        await SeedOrderWithEmailAndSmsNotifications(_recipientNin, requestedSendTime: new DateTime(2023, 06, 16, 08, 50, 00, DateTimeKind.Utc));

        DashboardRepository sut = GetRepository();

        // Act
        var result = await sut.GetDashboardNotificationsByNinAsync(
            _recipientNin,
            new DateTime(2023, 06, 01, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2023, 07, 01, 0, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(2, result[0].DeliveryAttempts.Count);
        Assert.Contains(result[0].DeliveryAttempts, r => r.Channel == "email");
        Assert.Contains(result[0].DeliveryAttempts, r => r.Channel == "sms");
    }

    private static DashboardRepository GetRepository() =>
        ServiceUtil.GetServices([typeof(IDashboardRepository)])
            .OfType<DashboardRepository>()
            .First();

    private async Task<Guid> SeedOrderWithEmailNotification(string recipientNin, DateTime requestedSendTime)
    {
        var orderRepo = ServiceUtil.GetServices([typeof(IOrderRepository)])
            .OfType<OrderRepository>()
            .First();
        var emailRepo = ServiceUtil.GetServices([typeof(IEmailNotificationRepository)])
            .OfType<EmailNotificationRepository>()
            .First();

        NotificationOrder order = TestdataUtil.NotificationOrder_EmailTemplate_OneRecipient();
        order.Id = Guid.NewGuid();
        order.RequestedSendTime = requestedSendTime;

        await orderRepo.Create(order);

        EmailNotification notification = new()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            RequestedSendTime = requestedSendTime,
            Recipient = new()
            {
                ToAddress = "recipient@example.com",
                NationalIdentityNumber = recipientNin
            },
            SendResult = new(EmailNotificationResultType.Succeeded, requestedSendTime)
        };

        await emailRepo.AddNotification(notification, requestedSendTime.AddDays(1));

        _orderIdsToDelete.Add(order.Id);
        return order.Id;
    }

    private async Task<Guid> SeedOrderWithEmailAndSmsNotifications(string recipientNin, DateTime requestedSendTime)
    {
        var orderRepo = ServiceUtil.GetServices([typeof(IOrderRepository)])
            .OfType<OrderRepository>()
            .First();
        var emailRepo = ServiceUtil.GetServices([typeof(IEmailNotificationRepository)])
            .OfType<EmailNotificationRepository>()
            .First();
        var smsRepo = ServiceUtil.GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        NotificationOrder order = TestdataUtil.NotificationOrder_EmailTemplate_OneRecipient();
        order.Id = Guid.NewGuid();
        order.RequestedSendTime = requestedSendTime;

        await orderRepo.Create(order);

        EmailNotification emailNotification = new()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            RequestedSendTime = requestedSendTime,
            Recipient = new()
            {
                ToAddress = "recipient@example.com",
                NationalIdentityNumber = recipientNin
            },
            SendResult = new(EmailNotificationResultType.Succeeded, requestedSendTime)
        };

        SmsNotification smsNotification = new()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            RequestedSendTime = requestedSendTime,
            Recipient = new()
            {
                MobileNumber = "+4799999999",
                NationalIdentityNumber = recipientNin
            },
            SendResult = new(SmsNotificationResultType.Accepted, requestedSendTime)
        };

        await emailRepo.AddNotification(emailNotification, requestedSendTime.AddDays(1));
        await smsRepo.AddNotification(smsNotification, requestedSendTime.AddDays(1));

        _orderIdsToDelete.Add(order.Id);
        return order.Id;
    }

    private async Task<Guid> SeedOrderWithSmsNotification(string recipientNin, DateTime requestedSendTime)
    {
        var orderRepo = ServiceUtil.GetServices([typeof(IOrderRepository)])
            .OfType<OrderRepository>()
            .First();
        var smsRepo = ServiceUtil.GetServices([typeof(ISmsNotificationRepository)])
            .OfType<SmsNotificationRepository>()
            .First();

        NotificationOrder order = TestdataUtil.NotificationOrder_SmsTemplate_OneRecipient();
        order.Id = Guid.NewGuid();
        order.RequestedSendTime = requestedSendTime;

        await orderRepo.Create(order);

        SmsNotification notification = new()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            RequestedSendTime = requestedSendTime,
            Recipient = new()
            {
                MobileNumber = "+4799999999",
                NationalIdentityNumber = recipientNin
            },
            SendResult = new(SmsNotificationResultType.Accepted, requestedSendTime)
        };

        await smsRepo.AddNotification(notification, requestedSendTime.AddDays(1));

        _orderIdsToDelete.Add(order.Id);
        return order.Id;
    }
}
