using System;
using System.Collections.Generic;

using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Dashboard;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Dashboard;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class DashboardMapperTests
{
    private static readonly Guid _notificationId = Guid.NewGuid();
    private const string _nin = "16069412345";

    [Fact]
    public void MapToDashboardNotificationExtList_EmptyList_ReturnsEmptyList()
    {
        var result = new List<DashboardNotification>().MapToDashboardNotificationExtList();

        Assert.Empty(result);
    }

    [Fact]
    public void MapToDashboardNotificationExtList_EmailNotification_MapsEmailAddressAndNullMobile()
    {
        var notification = BuildNotification(
            channel: "email",
            addressPoints: [new EmailAddressPoint("test@example.com")]);

        var result = notification.MapToDashboardNotificationExtList();

        var recipient = Assert.Single(Assert.Single(result).Recipients);
        Assert.Equal("test@example.com", recipient.EmailAddress);
        Assert.Null(recipient.MobileNumber);
    }

    [Fact]
    public void MapToDashboardNotificationExtList_SmsNotification_MapsMobileNumberAndNullEmail()
    {
        var notification = BuildNotification(
            channel: "sms",
            addressPoints: [new SmsAddressPoint("+4712345678")]);

        var result = notification.MapToDashboardNotificationExtList();

        var recipient = Assert.Single(Assert.Single(result).Recipients);
        Assert.Equal("+4712345678", recipient.MobileNumber);
        Assert.Null(recipient.EmailAddress);
    }

    [Fact]
    public void MapToDashboardNotificationExtList_NoAddressInfo_BothAddressFieldsNull()
    {
        var notification = BuildNotification(channel: "email", addressPoints: []);

        var result = notification.MapToDashboardNotificationExtList();

        var recipient = Assert.Single(Assert.Single(result).Recipients);
        Assert.Null(recipient.EmailAddress);
        Assert.Null(recipient.MobileNumber);
    }

    [Fact]
    public void MapToDashboardNotificationExtList_RecipientIdentifiersAreMapped()
    {
        var notification = BuildNotification(
            channel: "email",
            addressPoints: [new EmailAddressPoint("test@example.com")],
            organizationNumber: "991825827");

        var result = notification.MapToDashboardNotificationExtList();

        var recipient = Assert.Single(Assert.Single(result).Recipients);
        Assert.Equal(_nin, recipient.NationalIdentityNumber);
        Assert.Equal("991825827", recipient.OrganizationNumber);
    }

    [Fact]
    public void MapToDashboardNotificationExtList_AllScalarFieldsMapped()
    {
        var sendTime = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var resultTime = new DateTime(2026, 5, 1, 12, 5, 0, DateTimeKind.Utc);
        var notifications = new List<DashboardNotification>
        {
            new(
                _notificationId,
                "ttd",
                "urn:altinn:resource:app-ttd-test",
                "ref-123",
                sendTime,
                [new Recipient([new EmailAddressPoint("a@b.com")], nationalIdentityNumber: _nin)],
                "email",
                "Succeeded",
                resultTime),
        };

        var result = notifications.MapToDashboardNotificationExtList();

        var ext = Assert.Single(result);
        Assert.Equal(_notificationId, ext.NotificationId);
        Assert.Equal("ttd", ext.CreatorName);
        Assert.Equal("urn:altinn:resource:app-ttd-test", ext.ResourceId);
        Assert.Equal("ref-123", ext.SendersReference);
        Assert.Equal(sendTime, ext.RequestedSendTime);
        Assert.Equal("email", ext.Channel);
        Assert.Equal("Succeeded", ext.Result);
        Assert.Equal(resultTime, ext.ResultTime);
    }

    [Fact]
    public void MapToDashboardNotificationExtList_MultipleNotifications_AllMapped()
    {
        var notifications = new List<DashboardNotification>
        {
            BuildNotification("email", [new EmailAddressPoint("a@b.com")])[0],
            BuildNotification("sms", [new SmsAddressPoint("+4700000001")])[0],
        };

        var result = notifications.MapToDashboardNotificationExtList();

        Assert.Equal(2, result.Count);
        Assert.Equal("a@b.com", result[0].Recipients[0].EmailAddress);
        Assert.Equal("+4700000001", result[1].Recipients[0].MobileNumber);
    }

    private static List<DashboardNotification> BuildNotification(
        string channel,
        List<IAddressPoint> addressPoints,
        string? organizationNumber = null)
    {
        return
        [
            new DashboardNotification(
                _notificationId,
                "ttd",
                null,
                null,
                DateTime.UtcNow,
                [new Recipient(addressPoints, organizationNumber: organizationNumber, nationalIdentityNumber: _nin)],
                channel,
                null,
                null),
        ];
    }
}
