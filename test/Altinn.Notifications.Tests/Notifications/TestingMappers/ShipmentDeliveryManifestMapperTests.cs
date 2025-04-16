using System;
using System.Collections.Immutable;

using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Delivery;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class ShipmentDeliveryManifestMapperTests
{
    [Fact]
    public void MapToShipmentDeliveryManifestExt_ValidManifest_MapsAllProperties()
    {
        // Arrange
        var shipmentDeliveryManifest = new ShipmentDeliveryManifest
        {
            Status = "Delivered",
            Type = "Notification",
            SendersReference = "REF123",
            ShipmentId = Guid.NewGuid(),
            LastUpdate = DateTime.UtcNow,
            StatusDescription = "Successfully delivered",
            Recipients = ImmutableList<IDeliverableEntity>.Empty
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
    public void MapToShipmentDeliveryManifestExt_WithRecipients_MapsAllPropertiesAndRecipients()
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
            Status = "Delivered",
            LastUpdate = DateTime.UtcNow,
            Destination = "recipient@example.com",
            StatusDescription = "Email delivered to recipient"
        };

        var recipients = ImmutableList.Create<IDeliverableEntity>(smsDeliveryManifest, emailDeliveryManifest);

        var shipmentDeliveryManifest = new ShipmentDeliveryManifest
        {
            Status = "Delivered",
            Type = "Notification",
            Recipients = recipients,
            ShipmentId = Guid.NewGuid(),
            SendersReference = "REF456",
            LastUpdate = DateTime.UtcNow,
            StatusDescription = "Successfully delivered all messages"
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
        Assert.Equal(smsDeliveryManifest.Destination, smsResult.Destination);
        Assert.Equal(smsDeliveryManifest.StatusDescription, smsResult.StatusDescription);

        // Verify second recipient (Email)
        var emailResult = result.Recipients[1] as EmailDeliveryManifestExt;
        Assert.NotNull(emailResult);
        Assert.Equal(emailDeliveryManifest.Status, emailResult.Status);
        Assert.Equal(emailDeliveryManifest.Destination, emailResult.Destination);
        Assert.Equal(emailDeliveryManifest.StatusDescription, emailResult.StatusDescription);
    }
}
