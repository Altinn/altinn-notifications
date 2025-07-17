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
        var instantNotificationOrder = new InstantNotificationOrder
        {
            OrderId = Guid.NewGuid(),
            Created = DateTime.UtcNow,
            OrderChainId = Guid.NewGuid(),
            SendersReference = "Test reference",
            InstantNotificationRecipient = null!,
            Creator = new Creator("Test creator"),
            IdempotencyId = Guid.NewGuid().ToString()
        };

        Assert.Throws<ArgumentNullException>(() => instantNotificationOrder.MapToShortMessage("Default sender"));
    }

    [Fact]
    public void MapToShortMessage_WithValidInstantNotificationOrder_ShouldMapCorrectly()
    {
        var instantNotificationOrder = new InstantNotificationOrder
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

        var result = instantNotificationOrder.MapToShortMessage(defaultSenderNumber);

        Assert.NotEqual(defaultSenderNumber, result.Sender);

        Assert.Equal(instantNotificationOrder.OrderId, result.NotificationId);
        Assert.Equal(instantNotificationOrder.InstantNotificationRecipient.ShortMessageDeliveryDetails.PhoneNumber, result.Recipient);
        Assert.Equal(instantNotificationOrder.InstantNotificationRecipient.ShortMessageDeliveryDetails.TimeToLiveInSeconds, result.TimeToLive);
        Assert.Equal(instantNotificationOrder.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Sender, result.Sender);
        Assert.Equal(instantNotificationOrder.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Message, result.Message);
    }

    [Fact]
    public void MapToShortMessage_WithNullDeliveryDetails_ShouldThrowArgumentNullException()
    {
        var instantNotificationOrder = new InstantNotificationOrder
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

        Assert.Throws<ArgumentNullException>(() => instantNotificationOrder.MapToShortMessage("Default sender"));
    }

    [Fact]
    public void MapToShortMessage_WithNullShortMessageContent_ShouldThrowArgumentNullException()
    {
        var instantNotificationOrder = new InstantNotificationOrder
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

        Assert.Throws<ArgumentNullException>(() => instantNotificationOrder.MapToShortMessage("Default sender"));
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
    public void MapToShortMessage_WithInvalidDefaultSenderIdentifier_ShouldThrowException(string? defaultSender)
    {
        var instantNotificationOrder = new InstantNotificationOrder
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
            Assert.Throws<ArgumentException>(() => instantNotificationOrder.MapToShortMessage(defaultSender!));
        }
        else
        {
            Assert.Throws<ArgumentNullException>(() => instantNotificationOrder.MapToShortMessage(defaultSender!));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void MapToShortMessage_WithInvalidSenderIdentifier_UseDefaultSenderIdentifier_ShouldMapCorrectly(string? senderIdentifier)
    {
        var instantNotificationOrder = new InstantNotificationOrder
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

        var result = instantNotificationOrder.MapToShortMessage(defaultSenderNumber);

        Assert.Equal(defaultSenderNumber, result.Sender);
    }

    [Fact]
    public void MapToInstantNotificationOrder_WithNullRecipient_ShouldThrowArgumentNullException()
    {
        var instantNotificationOrderRequest = new InstantNotificationOrderRequestExt
        {
            SendersReference = "Test reference",
            InstantNotificationRecipient = null!,
            IdempotencyId = "Test idempotency identifier"
        };

        Assert.Throws<ArgumentNullException>(() => instantNotificationOrderRequest.MapToInstantNotificationOrder("Test creator", DateTime.UtcNow));
    }

    [Fact]
    public void MapToInstantNotificationOrder_WithNullDeliveryDetails_ShouldThrowArgumentNullException()
    {
        var instantNotificationOrderRequest = new InstantNotificationOrderRequestExt
        {
            SendersReference = "Test reference",
            IdempotencyId = "Test idempotency identifier",
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = null!
            }
        };

        Assert.Throws<ArgumentNullException>(() => instantNotificationOrderRequest.MapToInstantNotificationOrder("Test creator", DateTime.UtcNow));
    }

    [Fact]
    public void MapToInstantNotificationOrder_WithNullShortMessageContent_ShouldThrowArgumentNullException()
    {
        var instantNotificationOrderRequest = new InstantNotificationOrderRequestExt
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

        Assert.Throws<ArgumentNullException>(() => instantNotificationOrderRequest.MapToInstantNotificationOrder("Test creator", DateTime.UtcNow));
    }

    [Fact]
    public void MapToInstantNotificationOrder_WithValidInstantNotificationOrderRequestExt_ShouldMapCorrectly()
    {
        var creatorShortName = "Test creator";
        var instantNotificationOrderRequest = new InstantNotificationOrderRequestExt
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

        var result = instantNotificationOrderRequest.MapToInstantNotificationOrder(creatorShortName, created);

        Assert.Equal(created, result.Created);
        Assert.Equal(creatorShortName, result.Creator.ShortName);

        Assert.Equal(instantNotificationOrderRequest.IdempotencyId, result.IdempotencyId);
        Assert.Equal(instantNotificationOrderRequest.SendersReference, result.SendersReference);
        Assert.Equal(instantNotificationOrderRequest.InstantNotificationRecipient.ShortMessageDeliveryDetails.PhoneNumber, result.InstantNotificationRecipient.ShortMessageDeliveryDetails.PhoneNumber);
        Assert.Equal(instantNotificationOrderRequest.InstantNotificationRecipient.ShortMessageDeliveryDetails.TimeToLiveInSeconds, result.InstantNotificationRecipient.ShortMessageDeliveryDetails.TimeToLiveInSeconds);
        Assert.Equal(instantNotificationOrderRequest.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Body, result.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Message);
        Assert.Equal(instantNotificationOrderRequest.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Sender, result.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent.Sender);
    }

    [Fact]
    public void MapToInstantNotificationOrder_WithNullInstantNotificationOrderRequest_ShouldThrowArgumentNullException()
    {
        InstantNotificationOrderRequestExt? instantNotificationOrderRequest = null;

        Assert.Throws<ArgumentNullException>(() => instantNotificationOrderRequest!.MapToInstantNotificationOrder("Test creator", DateTime.UtcNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void MapToInstantNotificationOrder_WithInvalidCreatorShortName_ShouldThrowException(string? creatorShortName)
    {
        var instantNotificationOrderRequest = new InstantNotificationOrderRequestExt
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
            Assert.Throws<ArgumentException>(() => instantNotificationOrderRequest.MapToInstantNotificationOrder(creatorShortName!, DateTime.UtcNow));
        }
        else
        {
            Assert.Throws<ArgumentNullException>(() => instantNotificationOrderRequest.MapToInstantNotificationOrder(creatorShortName!, DateTime.UtcNow));
        }
    }

    [Fact]
    public void MapToInstantNotificationOrderResponse_WithNullTracking_ShouldThrowArgumentNullException()
    {
        InstantNotificationOrderTracking? instantNotificationOrderTracking = null;

        Assert.Throws<ArgumentNullException>(() => instantNotificationOrderTracking!.MapToInstantNotificationOrderResponse());
    }

    [Fact]
    public void MapToInstantNotificationOrderResponse_WithNullNotification_ShouldThrowArgumentNullException()
    {
        var instantNotificationOrderTracking = new InstantNotificationOrderTracking
        {
            Notification = null!,
            OrderChainId = Guid.NewGuid()
        };

        Assert.Throws<ArgumentNullException>(() => instantNotificationOrderTracking.MapToInstantNotificationOrderResponse());
    }

    [Fact]
    public void MapToInstantNotificationOrderResponse_WithValidInstantNotificationOrderTracking_ShouldMapCorrectly()
    {
        var instantNotificationOrderTracking = new InstantNotificationOrderTracking
        {
            OrderChainId = Guid.NewGuid(),
            Notification = new NotificationOrderChainShipment
            {
                ShipmentId = Guid.NewGuid(),
                SendersReference = "Test reference"
            }
        };

        var result = instantNotificationOrderTracking.MapToInstantNotificationOrderResponse();

        Assert.Equal(instantNotificationOrderTracking.OrderChainId, result.OrderChainId);
        Assert.Equal(instantNotificationOrderTracking.Notification.ShipmentId, result.Notification.ShipmentId);
        Assert.Equal(instantNotificationOrderTracking.Notification.SendersReference, result.Notification.SendersReference);
    }
}
