using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Delivery;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class NotificationDeliveryManifestMapperTests
{
    [Fact]
    public void MapToNotificationDeliveryManifestExt_WithRecipients_MapsCorrectly()
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
            Status = ProcessingLifecycle.Order_Completed,
            SendersReference = "74DDBD31-7C43-4AF0-8A8A-803DED6CCC6D"
        };

        // Act
        var result = notificationDeliveryManifest.MapToNotificationDeliveryManifestExt();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(notificationDeliveryManifest.Type, result.Type);
        Assert.Equal(notificationDeliveryManifest.LastUpdate, result.LastUpdate);
        Assert.Equal(notificationDeliveryManifest.ShipmentId, result.ShipmentId);
        Assert.Equal((int)notificationDeliveryManifest.Status, (int)result.Status);
        Assert.Equal(notificationDeliveryManifest.SequenceNumber, result.SequenceNumber);
        Assert.Equal(notificationDeliveryManifest.SendersReference, result.SendersReference);

        Assert.Equal(2, result.Recipients.Count);
        Assert.IsType<SmsDeliveryManifestExt>(result.Recipients[0]);
        Assert.IsType<EmailDeliveryManifestExt>(result.Recipients[1]);

        // Verify first recipient (SMS)
        var smsResult = result.Recipients[0] as SmsDeliveryManifestExt;
        Assert.NotNull(smsResult);
        Assert.Equal(smsDeliveryManifest.LastUpdate, smsResult.LastUpdate);
        Assert.Equal(smsDeliveryManifest.Destination, smsResult.Destination);
        Assert.Equal((int)smsDeliveryManifest.Status, (int)smsResult.Status);

        // Verify second recipient (Email)
        var emailResult = result.Recipients[1] as EmailDeliveryManifestExt;
        Assert.NotNull(emailResult);
        Assert.Equal(emailDeliveryManifest.LastUpdate, emailResult.LastUpdate);
        Assert.Equal(emailDeliveryManifest.Destination, emailResult.Destination);
        Assert.Equal((int)emailDeliveryManifest.Status, (int)emailResult.Status);
    }

    [Fact]
    public void MapToNotificationDeliveryManifestExt_WithoutRecipients_MapsCorrectly()
    {
        // Arrange
        var shipmentDeliveryManifest = new NotificationDeliveryManifest
        {
            Recipients = [],
            Type = "Notification",
            ShipmentId = Guid.NewGuid(),
            LastUpdate = DateTime.UtcNow,
            Status = ProcessingLifecycle.Order_Completed,
            SendersReference = "F883C29A-CA66-4830-B4A1-CB23B11F268D",
        };

        // Act
        var result = shipmentDeliveryManifest.MapToNotificationDeliveryManifestExt();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Recipients);
        Assert.Equal(shipmentDeliveryManifest.Type, result.Type);
        Assert.Equal(shipmentDeliveryManifest.LastUpdate, result.LastUpdate);
        Assert.Equal(shipmentDeliveryManifest.ShipmentId, result.ShipmentId);
        Assert.Equal((int)shipmentDeliveryManifest.Status, (int)result.Status);
        Assert.Equal(shipmentDeliveryManifest.SequenceNumber, result.SequenceNumber);
        Assert.Equal(shipmentDeliveryManifest.SendersReference, result.SendersReference);
    }

    [Fact]
    public void MapToNotificationDeliveryManifestExt_WithNullSendersReference_MapsCorrectly()
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
        Assert.Equal((int)shipmentDeliveryManifest.Status, (int)result.Status);
        Assert.Equal(shipmentDeliveryManifest.SequenceNumber, result.SequenceNumber);
    }

    [Fact]
    public void MapToNotificationDeliveryManifestExt_NullManifest_ThrowsArgumentNullException()
    {
        // Arrange
        INotificationDeliveryManifest? manifest = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(manifest!.MapToNotificationDeliveryManifestExt);
    }

    [Fact]
    public void MapToNotificationDeliveryManifestExt_NullSmsRecipient_ThrowsArgumentException()
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
        Assert.Throws<ArgumentException>(mockManifest.Object.MapToNotificationDeliveryManifestExt);
    }

    [Fact]
    public void MapToNotificationDeliveryManifestExt_UnsupportedDeliverableEntityType_ThrowsArgumentException()
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
}
