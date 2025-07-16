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
    public void MapToShortMessage_WithNullRecipient_ShouldThrowArgumentNullException()
    {
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

        Assert.Throws<ArgumentNullException>(() => domainModel.MapToShortMessage("Default sender"));
    }

    [Fact]
    public void MapToShortMessage_WithValidInstantNotificationOrder_ShouldMapCorrectly()
    {
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

        var result = domainModel.MapToShortMessage(defaultSenderNumber);

        Assert.NotEqual(defaultSenderNumber, result.Sender);

        Assert.Equal(domainModel.OrderId, result.NotificationId);
        Assert.Equal(domainModel.InstantNotificationRecipient.ShortMessageDeliveryDetails.PhoneNumber, result.Recipient);
        Assert.Equal(domainModel.InstantNotificationRecipient.ShortMessageDeliveryDetails.TimeToLiveInSeconds, result.TimeToLive);
        Assert.Equal(domainModel.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Sender, result.Sender);
        Assert.Equal(domainModel.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Message, result.Message);
    }

    [Fact]
    public void MapToShortMessage_WithNullDeliveryDetails_ShouldThrowArgumentNullException()
    {
        var domainModel = new InstantNotificationOrder
        {
            OrderId = Guid.NewGuid(),
            Created = DateTime.UtcNow,
            OrderChainId = Guid.NewGuid(),
            SendersReference = "Test reference",
            Creator = new Creator("Test creator"),
            IdempotencyId = Guid.NewGuid().ToString(),
            InstantNotificationRecipient = new InstantNotificationRecipient
            {
                ShortMessageDeliveryDetails = null!
            }
        };

        Assert.Throws<ArgumentNullException>(() => domainModel.MapToShortMessage("Default sender"));
    }

    [Fact]
    public void MapToShortMessage_WithNullShortMessageContent_ShouldThrowArgumentNullException()
    {
        var domainModel = new InstantNotificationOrder
        {
            OrderId = Guid.NewGuid(),
            Created = DateTime.UtcNow,
            OrderChainId = Guid.NewGuid(),
            SendersReference = "Test reference",
            Creator = new Creator("Test creator"),
            IdempotencyId = Guid.NewGuid().ToString(),
            InstantNotificationRecipient = new InstantNotificationRecipient
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetails
                {
                    PhoneNumber = "+4799999999",
                    TimeToLiveInSeconds = 3600,
                    ShortMessageContent = null!
                }
            }
        };

        Assert.Throws<ArgumentNullException>(() => domainModel.MapToShortMessage("Default sender"));
    }

    [Fact]
    public void MapToShortMessage_WithNullInstantNotificationOrder_ShouldThrowArgumentNullException()
    {
        InstantNotificationOrder? domainModel = null;
        Assert.Throws<ArgumentNullException>(() => domainModel!.MapToShortMessage("Default sender"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void MapToShortMessage_WithInvalidDefaultSenderIdentifier_ShouldThrowExceptions(string? defaultSender)
    {
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

        if (defaultSender is not null)
        {
            Assert.Throws<ArgumentException>(() => domainModel.MapToShortMessage(defaultSender!));
        }
        else
        {
            Assert.Throws<ArgumentNullException>(() => domainModel.MapToShortMessage(defaultSender!));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void MapToShortMessage_WithInvalidSenderIdentifier_UseDefaultSender_ShouldMapCorrectly(string? senderIdentifier)
    {
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
                        Message = "Test message",
                        Sender = senderIdentifier
                    }
                }
            }
        };
        var defaultSenderNumber = "Default sender";

        var result = domainModel.MapToShortMessage(defaultSenderNumber);

        Assert.Equal(defaultSenderNumber, result.Sender);
    }

    [Fact]
    public void MapToInstantNotificationOrder_WithNullRecipient_ShouldThrowArgumentNullException()
    {
        var requestModel = new InstantNotificationOrderRequestExt
        {
            SendersReference = "Test reference",
            InstantNotificationRecipient = null!,
            IdempotencyId = "Test idempotency identifier"
        };

        Assert.Throws<ArgumentNullException>(() => requestModel.MapToInstantNotificationOrder("Test creator", DateTime.UtcNow));
    }

    [Fact]
    public void MapToInstantNotificationOrder_WithNullDeliveryDetails_ShouldThrowArgumentNullException()
    {
        var requestModel = new InstantNotificationOrderRequestExt
        {
            SendersReference = "Test reference",
            IdempotencyId = "Test idempotency identifier",
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = null!
            }
        };

        Assert.Throws<ArgumentNullException>(() => requestModel.MapToInstantNotificationOrder("Test creator", DateTime.UtcNow));
    }

    [Fact]
    public void MapToInstantNotificationOrder_WithNullShortMessageContent_ShouldThrowArgumentNullException()
    {
        var requestModel = new InstantNotificationOrderRequestExt
        {
            SendersReference = "Test reference",
            IdempotencyId = "Test idempotency identifier",
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    TimeToLiveInSeconds = 3600,
                    ShortMessageContent = null!,
                    PhoneNumber = "+4799999999"
                }
            }
        };

        Assert.Throws<ArgumentNullException>(() => requestModel.MapToInstantNotificationOrder("Test creator", DateTime.UtcNow));
    }

    [Fact]
    public void MapToInstantNotificationOrder_WithValidInstantNotificationOrderRequestExt_ShouldMapCorrectly()
    {
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

        var created = DateTime.UtcNow;

        var mockDateTimeService = new Mock<IDateTimeService>();
        mockDateTimeService.Setup(e => e.UtcNow()).Returns(created);

        var result = requestModel.MapToInstantNotificationOrder(creatorShortName, created);

        Assert.Equal(created, result.Created);
        Assert.Equal(creatorShortName, result.Creator.ShortName);
        Assert.Equal(requestModel.IdempotencyId, result.IdempotencyId);
        Assert.Equal(requestModel.SendersReference, result.SendersReference);
        Assert.Equal(requestModel.InstantNotificationRecipient.ShortMessageDeliveryDetails.PhoneNumber, result.InstantNotificationRecipient.ShortMessageDeliveryDetails.PhoneNumber);
        Assert.Equal(requestModel.InstantNotificationRecipient.ShortMessageDeliveryDetails.TimeToLiveInSeconds, result.InstantNotificationRecipient.ShortMessageDeliveryDetails.TimeToLiveInSeconds);
        Assert.Equal(requestModel.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Body, result.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Message);
        Assert.Equal(requestModel.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Sender, result.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Sender);
    }

    [Fact]
    public void MapToInstantNotificationOrder_WithNullInstantNotificationOrderRequest_ShouldThrowArgumentNullException()
    {
        InstantNotificationOrderRequestExt? requestModel = null;

        Assert.Throws<ArgumentNullException>(() => requestModel!.MapToInstantNotificationOrder("Test creator", DateTime.UtcNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void MapToInstantNotificationOrder_WithInvalidCreatorShortName_ShouldThrowExceptions(string? creatorShortName)
    {
        var requestModel = new InstantNotificationOrderRequestExt
        {
            SendersReference = "Test reference",
            IdempotencyId = "Test idempotency identifier",
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    TimeToLiveInSeconds = 3600,
                    PhoneNumber = "+4799999999",
                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Body = "Test message",
                        Sender = "Test sender"
                    }
                }
            }
        };

        if (creatorShortName is not null)
        {
            Assert.Throws<ArgumentException>(() => requestModel.MapToInstantNotificationOrder(creatorShortName!, DateTime.UtcNow));
        }
        else
        {
            Assert.Throws<ArgumentNullException>(() => requestModel.MapToInstantNotificationOrder(creatorShortName!, DateTime.UtcNow));
        }
    }

    [Fact]
    public void MapToInstantNotificationOrderResponse_WithNullTracking_ShouldThrowArgumentNullException()
    {
        InstantNotificationOrderTracking? trackingModel = null;

        Assert.Throws<ArgumentNullException>(() => trackingModel!.MapToInstantNotificationOrderResponse());
    }

    [Fact]
    public void MapToInstantNotificationOrderResponse_WithNullNotification_ShouldThrowArgumentNullException()
    {
        var trackingModel = new InstantNotificationOrderTracking
        {
            Notification = null!,
            OrderChainId = Guid.NewGuid()
        };

        Assert.Throws<ArgumentNullException>(() => trackingModel.MapToInstantNotificationOrderResponse());
    }

    [Fact]
    public void MapToInstantNotificationOrderResponse_WithValidInstantNotificationOrderTracking_ShouldMapCorrectly()
    {
        var trackingModel = new InstantNotificationOrderTracking
        {
            OrderChainId = Guid.NewGuid(),
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = Guid.NewGuid(),
                SendersReference = "Test reference"
            }
        };

        var result = trackingModel.MapToInstantNotificationOrderResponse();

        Assert.Equal(trackingModel.OrderChainId, result.OrderChainId);
        Assert.Equal(trackingModel.Notification.ShipmentId, result.Notification.ShipmentId);
        Assert.Equal(trackingModel.Notification.SendersReference, result.Notification.SendersReference);
    }
}
