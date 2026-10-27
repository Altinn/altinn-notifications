using System.Collections.Immutable;

using Altinn.Notifications.Core.Models.NotificationLog;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.NotificationLog;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class NotificationLogMapperTests
{
    [Fact]
    public void MapToNotificationLogSummaryList_EmptyInput_ReturnsEmptyList()
    {
        // Arrange
        IImmutableList<NotificationLogSummary> entries = ImmutableList<NotificationLogSummary>.Empty;

        // Act
        var result = entries.MapToNotificationLogSummaryList();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void MapToNotificationLogSummaryList_MultipleEntries_MapsAllEntries()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var requestedSendTime1 = DateTime.UtcNow.AddMinutes(-20);
        var requestedSendTime2 = DateTime.UtcNow.AddMinutes(-10);
        var lastUpdateTime1 = DateTime.UtcNow.AddMinutes(-5);
        var lastUpdateTime2 = DateTime.UtcNow;

        IImmutableList<NotificationLogSummary> entries = ImmutableList.Create(
            new NotificationLogSummary
            {
                NotificationId = id1,
                Type = "Notification",
                Channel = "Email",
                Destination = "a@example.com",
                Status = "Succeeded",
                DialogId = "dialog-1",
                TransmissionId = "transmission-1",
                RequestedSendTime = requestedSendTime1,
                LastUpdateTime = lastUpdateTime1
            },
            new NotificationLogSummary
            {
                NotificationId = id2,
                Type = "Reminder",
                Channel = "Sms",
                Destination = "+4799999999",
                Status = "Failed",
                DialogId = "dialog-2",
                TransmissionId = "transmission-2",
                RequestedSendTime = requestedSendTime2,
                LastUpdateTime = lastUpdateTime2
            });

        // Act
        var result = entries.MapToNotificationLogSummaryList();

        // Assert
        Assert.Equal(2, result.Count);

        NotificationLogSummaryExt first = Assert.Single(result, e => e.NotificationId == id1);
        Assert.Equal("Email", first.Channel);
        Assert.Equal("Succeeded", first.Status);
        Assert.Equal("Notification", first.Type);
        Assert.Equal("dialog-1", first.DialogId);
        Assert.Equal("a@example.com", first.Destination);
        Assert.Equal(lastUpdateTime1, first.LastUpdateTime);
        Assert.Equal("transmission-1", first.TransmissionId);
        Assert.Equal(requestedSendTime1, first.RequestedSendTime);

        NotificationLogSummaryExt second = Assert.Single(result, e => e.NotificationId == id2);
        Assert.Equal("Sms", second.Channel);
        Assert.Equal("Failed", second.Status);
        Assert.Equal("Reminder", second.Type);
        Assert.Equal("dialog-2", second.DialogId);
        Assert.Equal("+4799999999", second.Destination);
        Assert.Equal(lastUpdateTime2, second.LastUpdateTime);
        Assert.Equal("transmission-2", second.TransmissionId);
        Assert.Equal(requestedSendTime2, second.RequestedSendTime);
    }

    [Fact]
    public void MapToNotificationLogSummaryList_SingleEntry_MapsAllFieldsCorrectly()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var lastUpdateTime = DateTime.UtcNow;
        var requestedSendTime = DateTime.UtcNow.AddMinutes(-10);

        IImmutableList<NotificationLogSummary> entries = ImmutableList.Create(new NotificationLogSummary
        {
            Channel = "Email",
            Status = "Succeeded",
            Type = "Notification",
            DialogId = "dialog-abc",
            LastUpdateTime = lastUpdateTime,
            NotificationId = notificationId,
            Destination = "user@example.com",
            TransmissionId = "transmission-xyz",
            RequestedSendTime = requestedSendTime
        });

        // Act
        var result = entries.MapToNotificationLogSummaryList();

        // Assert
        Assert.Single(result);

        NotificationLogSummaryExt ext = result[0];
        Assert.Equal("Email", ext.Channel);
        Assert.Equal("Succeeded", ext.Status);
        Assert.Equal("Notification", ext.Type);
        Assert.Equal("dialog-abc", ext.DialogId);
        Assert.Equal(lastUpdateTime, ext.LastUpdateTime);
        Assert.Equal(notificationId, ext.NotificationId);
        Assert.Equal("user@example.com", ext.Destination);
        Assert.Equal("transmission-xyz", ext.TransmissionId);
        Assert.Equal(requestedSendTime, ext.RequestedSendTime);
    }

    [Fact]
    public void MapToNotificationLogSummaryList_WithNullOptionalFields_MapsNullsCorrectly()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var lastUpdateTime = DateTime.UtcNow;
        var requestedSendTime = DateTime.UtcNow.AddMinutes(-10);

        IImmutableList<NotificationLogSummary> entries = ImmutableList.Create(new NotificationLogSummary
        {
            Channel = "Sms",
            Status = "Succeeded",
            Type = "Instant",
            DialogId = null,
            TransmissionId = null,
            NotificationId = notificationId,
            Destination = "+4799999999",
            LastUpdateTime = lastUpdateTime,
            RequestedSendTime = requestedSendTime
        });

        // Act
        var result = entries.MapToNotificationLogSummaryList();

        // Assert
        Assert.Single(result);

        NotificationLogSummaryExt ext = result[0];
        Assert.Null(ext.DialogId);
        Assert.Null(ext.TransmissionId);
        Assert.Equal("Sms", ext.Channel);
        Assert.Equal("Instant", ext.Type);
        Assert.Equal("Succeeded", ext.Status);
        Assert.Equal("+4799999999", ext.Destination);
        Assert.Equal(notificationId, ext.NotificationId);
        Assert.Equal(lastUpdateTime, ext.LastUpdateTime);
        Assert.Equal(requestedSendTime, ext.RequestedSendTime);
    }
}
