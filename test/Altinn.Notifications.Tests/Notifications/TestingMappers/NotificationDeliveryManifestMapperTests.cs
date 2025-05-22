using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Delivery;
using Altinn.Notifications.Models.Status;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class NotificationDeliveryManifestMapperTests
{
    [Fact]
    public void MapToNotificationDeliveryManifestExt_NullManifest_ThrowsArgumentNullException()
    {
        // Arrange
        INotificationDeliveryManifest? manifest = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(manifest!.MapToNotificationDeliveryManifestExt);
    }

    [Fact]
    public void MapToNotificationDeliveryManifestExt_WithProcessedOrder_ReturnsCompletelyMappedManifest()
    {
        // Arrange
        var smsDeliveryManifest = new SmsDeliveryManifest
        {
            Destination = "+4799999999",
            LastUpdate = DateTime.UtcNow,
            Status = ProcessingLifecycle.SMS_Delivered
        };

        var emailDeliveryManifest = new EmailDeliveryManifest
        {
            Destination = "recipient@example.com",
            Status = ProcessingLifecycle.Email_New,
            LastUpdate = DateTime.UtcNow.AddDays(10)
        };

        var recipients = ImmutableList.Create<IDeliveryManifest>(smsDeliveryManifest, emailDeliveryManifest);

        var notificationDeliveryManifest = new NotificationDeliveryManifest
        {
            Type = "Notification",
            Recipients = recipients,
            ShipmentId = Guid.NewGuid(),
            LastUpdate = DateTime.UtcNow,
            Status = ProcessingLifecycle.Order_Processed,
            SendersReference = "74DDBD31-7C43-4AF0-8A8A-803DED6CCC6D"
        };

        // Act
        var result = notificationDeliveryManifest.MapToNotificationDeliveryManifestExt();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(notificationDeliveryManifest.Type, result.Type);
        Assert.Equal(ProcessingLifecycleExt.Order_Processed, result.Status);
        Assert.Equal(notificationDeliveryManifest.LastUpdate, result.LastUpdate);
        Assert.Equal(notificationDeliveryManifest.ShipmentId, result.ShipmentId);
        Assert.Equal(notificationDeliveryManifest.SendersReference, result.SendersReference);

        Assert.Equal(2, result.Recipients.Count);
        Assert.IsType<SmsDeliveryManifestExt>(result.Recipients[0]);
        Assert.IsType<EmailDeliveryManifestExt>(result.Recipients[1]);

        // Verify first recipient (SMS)
        var smsResult = result.Recipients[0] as SmsDeliveryManifestExt;
        Assert.NotNull(smsResult);
        Assert.Equal(smsDeliveryManifest.LastUpdate, smsResult.LastUpdate);
        Assert.Equal(smsDeliveryManifest.Destination, smsResult.Destination);
        Assert.Equal(ProcessingLifecycleExt.SMS_Delivered, smsResult.Status);

        // Verify second recipient (Email)
        var emailResult = result.Recipients[1] as EmailDeliveryManifestExt;
        Assert.NotNull(emailResult);
        Assert.Equal(ProcessingLifecycleExt.Email_New, emailResult.Status);
        Assert.Equal(emailDeliveryManifest.LastUpdate, emailResult.LastUpdate);
        Assert.Equal(emailDeliveryManifest.Destination, emailResult.Destination);
    }

    [Fact]
    public void MapToNotificationDeliveryManifestExt_WithNullRecipientInList_ThrowsArgumentNullException()
    {
        // Arrange
        var mockManifest = new Mock<INotificationDeliveryManifest>();
        mockManifest.Setup(m => m.Type).Returns("Notification");
        mockManifest.Setup(m => m.ShipmentId).Returns(Guid.NewGuid());
        mockManifest.Setup(m => m.LastUpdate).Returns(DateTime.UtcNow);
        mockManifest.Setup(m => m.Status).Returns(ProcessingLifecycle.Order_Processing);

        var mockRecipients = new List<IDeliveryManifest> { null! }.ToImmutableList();
        mockManifest.Setup(m => m.Recipients).Returns(mockRecipients);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(mockManifest.Object.MapToNotificationDeliveryManifestExt);
    }

    [Fact]
    public void MapToNotificationDeliveryManifestExt_WithUnsupportedRecipientType_ThrowsArgumentException()
    {
        // Arrange
        var unknownDeliverableEntity = new Mock<IDeliveryManifest>();
        unknownDeliverableEntity.Setup(e => e.LastUpdate).Returns(DateTime.UtcNow);
        unknownDeliverableEntity.Setup(e => e.Destination).Returns("unknown destination");
        unknownDeliverableEntity.Setup(e => e.Status).Returns(ProcessingLifecycle.Order_Processing);

        var recipients = ImmutableList.Create(unknownDeliverableEntity.Object);

        var shipmentDeliveryManifest = new NotificationDeliveryManifest
        {
            Type = "Notification",
            Recipients = recipients,
            ShipmentId = Guid.NewGuid(),
            LastUpdate = DateTime.UtcNow,
            SendersReference = "TEST-UNSUPPORTED",
            Status = ProcessingLifecycle.Order_Processing
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(shipmentDeliveryManifest.MapToNotificationDeliveryManifestExt);
    }

    [Fact]
    public void MapToNotificationDeliveryManifestExt_WithRegisteredOrder_ReturnsManifestWithEmptyRecipients()
    {
        // Arrange
        var shipmentDeliveryManifest = new NotificationDeliveryManifest
        {
            Type = "Notification",
            ShipmentId = Guid.NewGuid(),
            LastUpdate = DateTime.UtcNow,
            Status = ProcessingLifecycle.Order_Registered,
            Recipients = ImmutableList<IDeliveryManifest>.Empty,
            SendersReference = "F883C29A-CA66-4830-B4A1-CB23B11F268D",
        };

        // Act
        var result = shipmentDeliveryManifest.MapToNotificationDeliveryManifestExt();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Recipients);
        Assert.Equal(shipmentDeliveryManifest.Type, result.Type);
        Assert.Equal(ProcessingLifecycleExt.Order_Registered, result.Status);
        Assert.Equal(shipmentDeliveryManifest.LastUpdate, result.LastUpdate);
        Assert.Equal(shipmentDeliveryManifest.ShipmentId, result.ShipmentId);
        Assert.Equal(shipmentDeliveryManifest.SendersReference, result.SendersReference);
    }

    [Fact]
    public void MapToNotificationDeliveryManifestExt_WithMultipleChannelRecipients_MapsAllRecipientsCorrectly()
    {
        // Arrange
        var emailDeliveryManifest = new EmailDeliveryManifest
        {
            LastUpdate = DateTime.UtcNow,
            Destination = "recipient@example.com",
            Status = ProcessingLifecycle.Email_Succeeded
        };

        var firstSmsDeliveryManifest = new SmsDeliveryManifest
        {
            Destination = "+4799999999",
            LastUpdate = DateTime.UtcNow.AddDays(-1),
            Status = ProcessingLifecycle.SMS_Delivered
        };

        var secondSmsDeliveryManifest = new SmsDeliveryManifest
        {
            Destination = "+4788888888",
            Status = ProcessingLifecycle.SMS_Failed,
            LastUpdate = DateTime.UtcNow.AddDays(-2)
        };

        var recipients = ImmutableList.Create<IDeliveryManifest>(firstSmsDeliveryManifest, emailDeliveryManifest, secondSmsDeliveryManifest);

        var manifest = new NotificationDeliveryManifest
        {
            Type = "Notification",
            Recipients = recipients,
            ShipmentId = Guid.NewGuid(),
            LastUpdate = DateTime.UtcNow,
            SendersReference = "REF-MIXED",
            Status = ProcessingLifecycle.Order_Processed
        };

        // Act
        var result = manifest.MapToNotificationDeliveryManifestExt();

        // Assert
        Assert.Equal(3, result.Recipients.Count);

        Assert.IsType<SmsDeliveryManifestExt>(result.Recipients[0]);
        var smsResult1 = result.Recipients[0] as SmsDeliveryManifestExt;
        Assert.Equal("+4799999999", smsResult1!.Destination);
        Assert.Equal(ProcessingLifecycleExt.SMS_Delivered, smsResult1.Status);

        Assert.IsType<EmailDeliveryManifestExt>(result.Recipients[1]);
        var emailResult = result.Recipients[1] as EmailDeliveryManifestExt;
        Assert.Equal("recipient@example.com", emailResult!.Destination);
        Assert.Equal(ProcessingLifecycleExt.Email_Succeeded, emailResult.Status);

        Assert.IsType<SmsDeliveryManifestExt>(result.Recipients[2]);
        var smsResult2 = result.Recipients[2] as SmsDeliveryManifestExt;
        Assert.Equal("+4788888888", smsResult2!.Destination);
        Assert.Equal(ProcessingLifecycleExt.SMS_Failed, smsResult2.Status);
    }

    [Fact]
    public void MapToNotificationDeliveryManifestExt_WithProcessingOrder_ReturnsManifestWithNullSendersReference()
    {
        // Arrange
        var shipmentDeliveryManifest = new NotificationDeliveryManifest
        {
            Type = "Notification",
            SendersReference = null,
            ShipmentId = Guid.NewGuid(),
            LastUpdate = DateTime.UtcNow,
            Status = ProcessingLifecycle.Order_Processing,
            Recipients = ImmutableList<IDeliveryManifest>.Empty
        };

        // Act
        var result = shipmentDeliveryManifest.MapToNotificationDeliveryManifestExt();

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.SendersReference);
        Assert.Equal(shipmentDeliveryManifest.Type, result.Type);
        Assert.Equal(shipmentDeliveryManifest.LastUpdate, result.LastUpdate);
        Assert.Equal(shipmentDeliveryManifest.ShipmentId, result.ShipmentId);
        Assert.Equal(ProcessingLifecycleExt.Order_Processing, result.Status);
    }

    [Fact]
    public void MapToNotificationDeliveryManifestExt_WithNullRecipients_ProcessingNotificationOrder_MapsToEmptyCollection()
    {
        // Arrange
        var shipmentDeliveryManifest = new NotificationDeliveryManifest
        {
            Recipients = null!,
            Type = "Notification",
            ShipmentId = Guid.NewGuid(),
            LastUpdate = DateTime.UtcNow,
            SendersReference = "REF-822D07A3FC64",
            Status = ProcessingLifecycle.Order_Processing
        };

        // Act
        var result = shipmentDeliveryManifest.MapToNotificationDeliveryManifestExt();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Recipients);
        Assert.Empty(result.Recipients);
    }

    [Theory]
    [InlineData(ProcessingLifecycle.Order_Completed, ProcessingLifecycleExt.Order_Completed)]
    [InlineData(ProcessingLifecycle.Order_Cancelled, ProcessingLifecycleExt.Order_Cancelled)]
    [InlineData(ProcessingLifecycle.Order_Processed, ProcessingLifecycleExt.Order_Processed)]
    [InlineData(ProcessingLifecycle.Order_Registered, ProcessingLifecycleExt.Order_Registered)]
    [InlineData(ProcessingLifecycle.Order_Processing, ProcessingLifecycleExt.Order_Processing)]
    [InlineData(ProcessingLifecycle.Order_SendConditionNotMet, ProcessingLifecycleExt.Order_SendConditionNotMet)]
    public void MapToNotificationDeliveryManifestExt_WithVariousOrderStatuses_MapsToCorrespondingExtendedEnums(ProcessingLifecycle status, ProcessingLifecycleExt expected)
    {
        // Arrange
        var manifest = new NotificationDeliveryManifest
        {
            Status = status,
            Type = "Notification",
            ShipmentId = Guid.NewGuid(),
            LastUpdate = DateTime.UtcNow,
            SendersReference = "REF-0D62AE8083B9",
            Recipients = ImmutableList<IDeliveryManifest>.Empty
        };

        // Act
        var result = manifest.MapToNotificationDeliveryManifestExt();

        // Assert
        Assert.Equal(expected, result.Status);
    }

    [Theory]
    [InlineData(ProcessingLifecycle.SMS_New, ProcessingLifecycleExt.SMS_New)]
    [InlineData(ProcessingLifecycle.SMS_Failed, ProcessingLifecycleExt.SMS_Failed)]
    [InlineData(ProcessingLifecycle.SMS_Sending, ProcessingLifecycleExt.SMS_Sending)]
    [InlineData(ProcessingLifecycle.SMS_Accepted, ProcessingLifecycleExt.SMS_Accepted)]
    [InlineData(ProcessingLifecycle.SMS_Delivered, ProcessingLifecycleExt.SMS_Delivered)]
    [InlineData(ProcessingLifecycle.SMS_Failed_Deleted, ProcessingLifecycleExt.SMS_Failed_Deleted)]
    [InlineData(ProcessingLifecycle.SMS_Failed_Expired, ProcessingLifecycleExt.SMS_Failed_Expired)]
    [InlineData(ProcessingLifecycle.SMS_Failed_Rejected, ProcessingLifecycleExt.SMS_Failed_Rejected)]
    [InlineData(ProcessingLifecycle.SMS_Failed_Undelivered, ProcessingLifecycleExt.SMS_Failed_Undelivered)]
    [InlineData(ProcessingLifecycle.SMS_Failed_BarredReceiver, ProcessingLifecycleExt.SMS_Failed_BarredReceiver)]
    [InlineData(ProcessingLifecycle.SMS_Failed_InvalidRecipient, ProcessingLifecycleExt.SMS_Failed_InvalidRecipient)]
    [InlineData(ProcessingLifecycle.SMS_Failed_RecipientReserved, ProcessingLifecycleExt.SMS_Failed_RecipientReserved)]
    [InlineData(ProcessingLifecycle.SMS_Failed_RecipientNotIdentified, ProcessingLifecycleExt.SMS_Failed_RecipientNotIdentified)]
    public void MapToNotificationDeliveryManifestExt_WithVariousSmsStatuses_MapsToCorrespondingExtendedEnums(ProcessingLifecycle status, ProcessingLifecycleExt expected)
    {
        // Arrange
        var smsDeliveryManifest = new SmsDeliveryManifest
        {
            Status = status,
            Destination = "+4799999999",
            LastUpdate = DateTime.UtcNow
        };

        var recipients = ImmutableList.Create<IDeliveryManifest>(smsDeliveryManifest);

        var manifest = new NotificationDeliveryManifest
        {
            Type = "Notification",
            Recipients = recipients,
            ShipmentId = Guid.NewGuid(),
            LastUpdate = DateTime.UtcNow,
            SendersReference = "REF-8957DE972471",
            Status = ProcessingLifecycle.Order_Completed
        };

        // Act
        var result = manifest.MapToNotificationDeliveryManifestExt();

        // Assert
        var smsResult = result.Recipients[0] as SmsDeliveryManifestExt;
        Assert.NotNull(smsResult);
        Assert.Equal(expected, smsResult.Status);
    }

    [Theory]
    [InlineData(ProcessingLifecycle.Email_New, ProcessingLifecycleExt.Email_New)]
    [InlineData(ProcessingLifecycle.Email_Failed, ProcessingLifecycleExt.Email_Failed)]
    [InlineData(ProcessingLifecycle.Email_Sending, ProcessingLifecycleExt.Email_Sending)]
    [InlineData(ProcessingLifecycle.Email_Succeeded, ProcessingLifecycleExt.Email_Succeeded)]
    [InlineData(ProcessingLifecycle.Email_Delivered, ProcessingLifecycleExt.Email_Delivered)]
    [InlineData(ProcessingLifecycle.Email_Failed_Bounced, ProcessingLifecycleExt.Email_Failed_Bounced)]
    [InlineData(ProcessingLifecycle.Email_Failed_Quarantined, ProcessingLifecycleExt.Email_Failed_Quarantined)]
    [InlineData(ProcessingLifecycle.Email_Failed_FilteredSpam, ProcessingLifecycleExt.Email_Failed_FilteredSpam)]
    [InlineData(ProcessingLifecycle.Email_Failed_InvalidFormat, ProcessingLifecycleExt.Email_Failed_InvalidFormat)]
    [InlineData(ProcessingLifecycle.Email_Failed_TransientError, ProcessingLifecycleExt.Email_Failed_TransientError)]
    [InlineData(ProcessingLifecycle.Email_Failed_RecipientReserved, ProcessingLifecycleExt.Email_Failed_RecipientReserved)]
    [InlineData(ProcessingLifecycle.Email_Failed_SuppressedRecipient, ProcessingLifecycleExt.Email_Failed_SuppressedRecipient)]
    [InlineData(ProcessingLifecycle.Email_Failed_RecipientNotIdentified, ProcessingLifecycleExt.Email_Failed_RecipientNotIdentified)]
    public void MapToNotificationDeliveryManifestExt_WithVariousEmailStatuses_MapsToCorrespondingExtendedEnums(ProcessingLifecycle status, ProcessingLifecycleExt expected)
    {
        // Arrange
        var emailDeliveryManifest = new EmailDeliveryManifest
        {
            Status = status,
            LastUpdate = DateTime.UtcNow,
            Destination = "test@example.com"
        };

        var recipients = ImmutableList.Create<IDeliveryManifest>(emailDeliveryManifest);

        var manifest = new NotificationDeliveryManifest
        {
            Type = "Notification",
            Recipients = recipients,
            SendersReference = "REF123",
            ShipmentId = Guid.NewGuid(),
            LastUpdate = DateTime.UtcNow,
            Status = ProcessingLifecycle.Order_Completed
        };

        // Act
        var result = manifest.MapToNotificationDeliveryManifestExt();

        // Assert
        var emailResult = result.Recipients[0] as EmailDeliveryManifestExt;
        Assert.NotNull(emailResult);
        Assert.Equal(expected, emailResult.Status);
    }
}
