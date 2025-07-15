using System;

using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class InstantNotificationOrderMapperTests
{
    [Fact]
    public void MapToShortMessage_NullInput_ShouldThrowArgumentNullException()
    {
        // Arrange
        InstantNotificationOrder? domainModel = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => domainModel!.MapToShortMessage("Default sender"));
    }

    [Fact]
    public void MapToShortMessage_WithValidInstantNotificationOrder_ShouldMapCorrectly()
    {
        // Arrange
        var domainModel = new InstantNotificationOrder
        {
            OrderId = Guid.NewGuid(),
            OrderChainId = Guid.NewGuid(),
            Creator = new Creator("Test creator"),
            IdempotencyId = Guid.NewGuid().ToString(),
            InstantNotificationRecipient = new InstantNotificationRecipient
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                {
                    TimeToLiveInSeconds = 3600,
                    PhoneNumber = "+4799999999",
                    ShortMessageContent = new ShortMessageContent
                    {
                        Sender = "Test sender",
                        Message = "Test message"
                    }
                }
            }
        };
        var defaultSenderNumber = "Default sender";

        // Act
        var result = domainModel.MapToShortMessage(defaultSenderNumber);

        // Assert
        Assert.Equal(domainModel.OrderId, result.NotificationId);
        Assert.Equal(domainModel.InstantNotificationRecipient.ShortMessageDeliveryDetails.PhoneNumber, result.Recipient);
        Assert.Equal(domainModel.InstantNotificationRecipient.ShortMessageDeliveryDetails.TimeToLiveInSeconds, result.TimeToLive);
        Assert.Equal(domainModel.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Sender, result.Sender);
        Assert.Equal(domainModel.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Message, result.Message);
    }

    [Fact]
    public void MapToShortMessage_InstantNotificationOrderWithNullRecipient_ShouldThrowArgumentNullException()
    {
        // Arrange
        var domainModel = new InstantNotificationOrder
        {
            OrderId = Guid.NewGuid(),
            Created = DateTime.UtcNow,
            OrderChainId = Guid.NewGuid(),
            SendersReference = "Test reference",
            InstantNotificationRecipient = null!,
            Creator = new Creator("Test creator"),
            IdempotencyId = Guid.NewGuid().ToString()
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => domainModel.MapToShortMessage("Default sender"));
    }

    [Fact]
    public void MapToInstantNotificationOrder_WithValidInstantNotificationOrderRequestExt_ShouldMapCorrectly()
    {
        // Arrange
        var creatorShortName = "Test creator";

        var requestModel = new InstantNotificationOrderRequestExt
        {
            SendersReference = "Test reference",
            IdempotencyId = "Test idempotency identifier",

            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    PhoneNumber = "+4799999999",
                    TimeToLiveInSeconds = 3600,

                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Body = "Test message",
                        Sender = "Test sender"
                    }
                }
            }
        };

        var mockDateTimeService = new Mock<IDateTimeService>();
        mockDateTimeService.Setup(e => e.UtcNow()).Returns(DateTime.UtcNow);

        // Act
        var result = requestModel.MapToInstantNotificationOrder(creatorShortName, mockDateTimeService.Object.UtcNow());

        // Assert
        Assert.Equal(requestModel.IdempotencyId, result.IdempotencyId);
        Assert.Equal(requestModel.SendersReference, result.SendersReference);
        Assert.Equal(requestModel.InstantNotificationRecipient.ShortMessageDeliveryDetails.PhoneNumber, result.InstantNotificationRecipient.ShortMessageDeliveryDetails.PhoneNumber);
        Assert.Equal(requestModel.InstantNotificationRecipient.ShortMessageDeliveryDetails.TimeToLiveInSeconds, result.InstantNotificationRecipient.ShortMessageDeliveryDetails.TimeToLiveInSeconds);
        Assert.Equal(requestModel.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Body, result.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Message);
        Assert.Equal(requestModel.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Sender, result.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Sender);
    }

    [Fact]
    public void MapToInstantNotificationOrder_WithNullInstantNotificationOrderRequestExt_ShouldThrowArgumentNullException()
    {
        // Arrange
        InstantNotificationOrderRequestExt? requestModel = null;

        var mockDateTimeService = new Mock<IDateTimeService>();
        mockDateTimeService.Setup(e => e.UtcNow()).Returns(DateTime.UtcNow);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => requestModel!.MapToInstantNotificationOrder("Test creator", mockDateTimeService.Object.UtcNow()));
    }

    [Fact]
    public void MapToInstantNotificationOrder_InstantNotificationOrderRequetWithNullCreatorShortName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var requestModel = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = "E7F9D8CF-AD4E-4A82-AEDA-0D47A0538C8D",
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    PhoneNumber = "+4799999999",
                    TimeToLiveInSeconds = 3600,
                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Body = "Test message",
                        Sender = "Test sender"
                    }
                }
            }
        };

        var mockDateTimeService = new Mock<IDateTimeService>();
        mockDateTimeService.Setup(e => e.UtcNow()).Returns(DateTime.UtcNow);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => requestModel.MapToInstantNotificationOrder(null!, mockDateTimeService.Object.UtcNow()));
    }

    [Fact]
    public void MapToInstantNotificationOrderResponse_WithValidInstantNotificationOrderTracking_ShouldMapCorrectly()
    {
        // Arrange
        var trackingModel = new InstantNotificationOrderTracking
        {
            OrderChainId = Guid.NewGuid(),
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = Guid.NewGuid(),
                SendersReference = "Test reference"
            }
        };

        // Act
        var result = trackingModel.MapToInstantNotificationOrderResponse();

        // Assert
        Assert.Equal(trackingModel.OrderChainId, result.OrderChainId);
        Assert.Equal(trackingModel.Notification.ShipmentId, result.Notification.ShipmentId);
        Assert.Equal(trackingModel.Notification.SendersReference, result.Notification.SendersReference);
    }

    [Fact]
    public void MapToInstantNotificationOrderRespons_WithNullInstantNotificationOrderTracking_ShouldThrowArgumentNullException()
    {
        // Arrange
        InstantNotificationOrderTracking? trackingModel = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => trackingModel!.MapToInstantNotificationOrderResponse());
    }
}
