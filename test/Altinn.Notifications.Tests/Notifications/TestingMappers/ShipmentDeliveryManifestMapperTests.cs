using System;
using System.Collections.Immutable;

using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Delivery;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class ShipmentDeliveryManifestMapperTests
{
    [Fact]
    public void MapToShipmentDeliveryManifestExt_WithRecipients_MapsCorrectly()
    {
        // Arrange
        var smsDeliveryManifest = new SmsDeliveryManifest
        {
            Status = "Delivered",
            Destination = "+4799999999",
            LastUpdate = DateTime.UtcNow,
            StatusDescription = "Message delivered to recipient"
        };

        var emailDeliveryManifest = new EmailDeliveryManifest
        {
            Status = "New",
            Destination = "recipient@example.com",
            LastUpdate = DateTime.UtcNow.AddDays(10),
            StatusDescription = "Email will be delivered to recipient on time"
        };

        var recipients = ImmutableList.Create<IDeliverableEntity>(smsDeliveryManifest, emailDeliveryManifest);

        var shipmentDeliveryManifest = new ShipmentDeliveryManifest
        {
            Status = "Processing",
            Type = "Notification",
            Recipients = recipients,
            ShipmentId = Guid.NewGuid(),
            LastUpdate = DateTime.UtcNow,
            SendersReference = "74DDBD31-7C43-4AF0-8A8A-803DED6CCC6D",
            StatusDescription = "Notification processing has started"
        };

        // Act
        var result = shipmentDeliveryManifest.MapToShipmentDeliveryManifestExt();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(shipmentDeliveryManifest.Type, result.Type);
        Assert.Equal(shipmentDeliveryManifest.Status, result.Status);
        Assert.Equal(shipmentDeliveryManifest.LastUpdate, result.LastUpdate);
        Assert.Equal(shipmentDeliveryManifest.ShipmentId, result.ShipmentId);
        Assert.Equal(shipmentDeliveryManifest.SendersReference, result.SendersReference);
        Assert.Equal(shipmentDeliveryManifest.StatusDescription, result.StatusDescription);

        Assert.Equal(2, result.Recipients.Count);
        Assert.IsType<SmsDeliveryManifestExt>(result.Recipients[0]);
        Assert.IsType<EmailDeliveryManifestExt>(result.Recipients[1]);

        // Verify first recipient (SMS)
        var smsResult = result.Recipients[0] as SmsDeliveryManifestExt;
        Assert.NotNull(smsResult);
        Assert.Equal(smsDeliveryManifest.Status, smsResult.Status);
        Assert.Equal(smsDeliveryManifest.LastUpdate, smsResult.LastUpdate);
        Assert.Equal(smsDeliveryManifest.Destination, smsResult.Destination);
        Assert.Equal(smsDeliveryManifest.StatusDescription, smsResult.StatusDescription);

        // Verify second recipient (Email)
        var emailResult = result.Recipients[1] as EmailDeliveryManifestExt;
        Assert.NotNull(emailResult);
        Assert.Equal(emailDeliveryManifest.Status, emailResult.Status);
        Assert.Equal(emailDeliveryManifest.LastUpdate, emailResult.LastUpdate);
        Assert.Equal(emailDeliveryManifest.Destination, emailResult.Destination);
        Assert.Equal(emailDeliveryManifest.StatusDescription, emailResult.StatusDescription);
    }

    [Fact]
    public void MapToShipmentDeliveryManifestExt_WithoutRecipients_MapsCorrectly()
    {
        // Arrange
        var shipmentDeliveryManifest = new ShipmentDeliveryManifest
        {
            Recipients = [],
            Status = "Delivered",
            Type = "Notification",
            ShipmentId = Guid.NewGuid(),
            LastUpdate = DateTime.UtcNow,
            StatusDescription = "Successfully delivered",
            SendersReference = "F883C29A-CA66-4830-B4A1-CB23B11F268D",
        };

        // Act
        var result = shipmentDeliveryManifest.MapToShipmentDeliveryManifestExt();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Recipients);
        Assert.Equal(shipmentDeliveryManifest.Type, result.Type);
        Assert.Equal(shipmentDeliveryManifest.Status, result.Status);
        Assert.Equal(shipmentDeliveryManifest.LastUpdate, result.LastUpdate);
        Assert.Equal(shipmentDeliveryManifest.ShipmentId, result.ShipmentId);
        Assert.Equal(shipmentDeliveryManifest.SendersReference, result.SendersReference);
        Assert.Equal(shipmentDeliveryManifest.StatusDescription, result.StatusDescription);
    }

    [Fact]
    public void MapToShipmentDeliveryManifestExt_WithNullSendersReference_MapsCorrectly()
    {
        // Arrange
        var shipmentDeliveryManifest = new ShipmentDeliveryManifest
        {
            Status = "Processing",
            Type = "Notification",
            SendersReference = null, // Explicitly set to null
            ShipmentId = Guid.NewGuid(),
            LastUpdate = DateTime.UtcNow,
            StatusDescription = "In progress",
            Recipients = ImmutableList<IDeliverableEntity>.Empty
        };

        // Act
        var result = shipmentDeliveryManifest.MapToShipmentDeliveryManifestExt();

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.SendersReference);
        Assert.Equal(shipmentDeliveryManifest.Type, result.Type);
        Assert.Equal(shipmentDeliveryManifest.Status, result.Status);
        Assert.Equal(shipmentDeliveryManifest.LastUpdate, result.LastUpdate);
        Assert.Equal(shipmentDeliveryManifest.ShipmentId, result.ShipmentId);
        Assert.Equal(shipmentDeliveryManifest.StatusDescription, result.StatusDescription);
    }

    [Fact]
    public void MapToShipmentDeliveryManifestExt_WithNullStatusDescription_MapsCorrectly()
    {
        // Arrange
        var shipmentDeliveryManifest = new ShipmentDeliveryManifest
        {
            Status = "Processing",
            Type = "Notification",
            SendersReference = "REF123",
            ShipmentId = Guid.NewGuid(),
            LastUpdate = DateTime.UtcNow,
            StatusDescription = null, // Explicitly set to null
            Recipients = ImmutableList<IDeliverableEntity>.Empty
        };

        // Act
        var result = shipmentDeliveryManifest.MapToShipmentDeliveryManifestExt();

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.StatusDescription);
        Assert.Equal(shipmentDeliveryManifest.Type, result.Type);
        Assert.Equal(shipmentDeliveryManifest.Status, result.Status);
        Assert.Equal(shipmentDeliveryManifest.LastUpdate, result.LastUpdate);
        Assert.Equal(shipmentDeliveryManifest.ShipmentId, result.ShipmentId);
        Assert.Equal(shipmentDeliveryManifest.SendersReference, result.SendersReference);
    }

    [Fact]
    public void MapToShipmentDeliveryManifestExt_UnsupportedDeliverableEntityType_ThrowsArgumentException()
    {
        // Arrange
        var mockEntity = new Mock<IDeliverableEntity>();
        mockEntity.Setup(e => e.Status).Returns("Unknown");
        mockEntity.Setup(e => e.LastUpdate).Returns(DateTime.UtcNow);
        mockEntity.Setup(e => e.Destination).Returns("unknown destination");

        var recipients = ImmutableList.Create(mockEntity.Object);

        var shipmentDeliveryManifest = new ShipmentDeliveryManifest
        {
            Status = "Processing",
            Type = "Notification",
            Recipients = recipients,
            ShipmentId = Guid.NewGuid(),
            LastUpdate = DateTime.UtcNow,
            StatusDescription = "Processing",
            SendersReference = "TEST-UNSUPPORTED"
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(shipmentDeliveryManifest.MapToShipmentDeliveryManifestExt);

        Assert.Equal("deliverableEntity", exception.ParamName);
        Assert.StartsWith("Unsupported deliverable entity type:", exception.Message);
    }
}
