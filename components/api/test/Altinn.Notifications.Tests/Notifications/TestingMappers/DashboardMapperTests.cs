using System;
using System.Collections.Generic;

using Altinn.Notifications.Core.Models.Dashboard;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Dashboard;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class DashboardMapperTests
{
    private static readonly Guid _shipmentId = Guid.NewGuid();
    private const string _nin = "16069412345";

    [Fact]
    public void MapToDashboardNotificationExtList_EmptyList_ReturnsEmptyList()
    {
        var result = new List<DashboardNotification>().MapToDashboardNotificationExtList();

        Assert.Empty(result);
    }

    [Fact]
    public void MapToDashboardNotificationExtList_EmailRecipient_MapsEmailAddressAndNullMobile()
    {
        var notification = BuildNotification([new DashboardDeliveryAttempt(_nin, "email", "test@example.com", null, null, null)]);

        var result = notification.MapToDashboardNotificationExtList();

        var recipient = Assert.Single(Assert.Single(result).DeliveryAttempts);
        Assert.Equal("test@example.com", recipient.EmailAddress);
        Assert.Null(recipient.MobileNumber);
    }

    [Fact]
    public void MapToDashboardNotificationExtList_SmsRecipient_MapsMobileNumberAndNullEmail()
    {
        var notification = BuildNotification([new DashboardDeliveryAttempt(_nin, "sms", null, "+4712345678", null, null)]);

        var result = notification.MapToDashboardNotificationExtList();

        var recipient = Assert.Single(Assert.Single(result).DeliveryAttempts);
        Assert.Equal("+4712345678", recipient.MobileNumber);
        Assert.Null(recipient.EmailAddress);
    }

    [Fact]
    public void MapToDashboardNotificationExtList_RecipientNinIsMapped()
    {
        var notification = BuildNotification([new DashboardDeliveryAttempt(_nin, "email", "test@example.com", null, null, null)]);

        var result = notification.MapToDashboardNotificationExtList();

        var recipient = Assert.Single(Assert.Single(result).DeliveryAttempts);
        Assert.Equal(_nin, recipient.NationalIdentityNumber);
    }

    [Fact]
    public void MapToDashboardNotificationExtList_AllScalarFieldsMapped()
    {
        var sendTime = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var resultTime = new DateTime(2026, 5, 1, 12, 5, 0, DateTimeKind.Utc);
        var notifications = new List<DashboardNotification>
        {
            new(
                _shipmentId,
                "ttd",
                "urn:altinn:resource:app-ttd-test",
                "ref-123",
                sendTime,
                "EmailPreferred",
                [new DashboardDeliveryAttempt(_nin, "email", "a@b.com", null, "Succeeded", resultTime)]),
        };

        var result = notifications.MapToDashboardNotificationExtList();

        var ext = Assert.Single(result);
        Assert.Equal(_shipmentId, ext.ShipmentId);
        Assert.Equal("ttd", ext.CreatorName);
        Assert.Equal("urn:altinn:resource:app-ttd-test", ext.ResourceId);
        Assert.Equal("ref-123", ext.SendersReference);
        Assert.Equal(sendTime, ext.RequestedSendTime);
        Assert.Equal("EmailPreferred", ext.NotificationChannel);
    }

    [Fact]
    public void MapToDashboardNotificationExtList_RecipientResultFieldsMapped()
    {
        var resultTime = new DateTime(2026, 5, 1, 12, 5, 0, DateTimeKind.Utc);
        var notification = BuildNotification([new DashboardDeliveryAttempt(_nin, "email", "a@b.com", null, "Succeeded", resultTime)]);

        var result = notification.MapToDashboardNotificationExtList();

        var recipient = Assert.Single(Assert.Single(result).DeliveryAttempts);
        Assert.Equal("Succeeded", recipient.Result);
        Assert.Equal(resultTime, recipient.ResultTime);
    }

    [Fact]
    public void MapToDashboardNotificationExtList_MultipleDeliveryAttempts_AllMapped()
    {
        var notification = BuildNotification([
            new DashboardDeliveryAttempt(_nin, "email", "a@b.com", null, null, null),
            new DashboardDeliveryAttempt(_nin, "sms", null, "+4700000001", null, null),
        ]);

        var result = notification.MapToDashboardNotificationExtList();

        var recipients = Assert.Single(result).DeliveryAttempts;
        Assert.Equal(2, recipients.Count);
        Assert.Equal("a@b.com", recipients[0].EmailAddress);
        Assert.Equal("+4700000001", recipients[1].MobileNumber);
    }

    private static List<DashboardNotification> BuildNotification(List<DashboardDeliveryAttempt> recipients)
    {
        return
        [
            new DashboardNotification(
                _shipmentId,
                "ttd",
                null,
                null,
                DateTime.UtcNow,
                null,
                recipients),
        ];
    }
}
