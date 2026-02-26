using System;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Models.Sms;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

/// <summary>
/// Tests for mapping flattened SMS notification order models.
/// </summary>
public class InstantSmsNotificationOrderMapperTests
{
    [Fact]
    public void MapToInstantSmsNotificationOrder_WithValidRequest_MapsCorrectly()
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;
        var idempotencyId = "test-idempotency-id";
        var sendersReference = "test-senders-reference";
        var phoneNumber = "+4799999999";
        var timeToLive = 3600;
        var messageBody = "Test SMS message";
        var sender = "TestSender";

        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = idempotencyId,
            SendersReference = sendersReference,
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = phoneNumber,
                TimeToLiveInSeconds = timeToLive,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = messageBody,
                    Sender = sender
                }
            }
        };

        // Act
        var result = request.MapToInstantSmsNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created, result.Created);
        Assert.Equal(creatorShortName, result.Creator.ShortName);
        Assert.Equal(idempotencyId, result.IdempotencyId);
        Assert.Equal(sendersReference, result.SendersReference);
        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.NotEqual(Guid.Empty, result.OrderChainId);

        Assert.NotNull(result.ShortMessageDeliveryDetails);
        Assert.Equal(phoneNumber, result.ShortMessageDeliveryDetails.PhoneNumber);
        Assert.Equal(timeToLive, result.ShortMessageDeliveryDetails.TimeToLiveInSeconds);

        Assert.NotNull(result.ShortMessageDeliveryDetails.ShortMessageContent);
        Assert.Equal(messageBody, result.ShortMessageDeliveryDetails.ShortMessageContent.Message);
        Assert.Equal(sender, result.ShortMessageDeliveryDetails.ShortMessageContent.Sender);
    }

    [Fact]
    public void MapToInstantSmsNotificationOrder_WithNullSender_MapsCorrectly()
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;
        var phoneNumber = "+4799999999";
        var messageBody = "Test SMS message";

        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = phoneNumber,
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = messageBody,
                    Sender = null // Null sender
                }
            }
        };

        // Act
        var result = request.MapToInstantSmsNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ShortMessageDeliveryDetails.ShortMessageContent);
        Assert.Equal(messageBody, result.ShortMessageDeliveryDetails.ShortMessageContent.Message);
        Assert.Null(result.ShortMessageDeliveryDetails.ShortMessageContent.Sender);
    }

    [Fact]
    public void MapToInstantSmsNotificationOrder_WithEmptySender_MapsCorrectly()
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;
        var phoneNumber = "+4799999999";
        var messageBody = "Test SMS message";

        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = phoneNumber,
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = messageBody,
                    Sender = string.Empty // Empty sender
                }
            }
        };

        // Act
        var result = request.MapToInstantSmsNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ShortMessageDeliveryDetails.ShortMessageContent);
        Assert.Equal(messageBody, result.ShortMessageDeliveryDetails.ShortMessageContent.Message);
        Assert.Equal(string.Empty, result.ShortMessageDeliveryDetails.ShortMessageContent.Sender);
    }

    [Fact]
    public void MapToInstantSmsNotificationOrder_WithoutSendersReference_MapsCorrectly()
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;
        var phoneNumber = "+4799999999";
        var messageBody = "Test SMS message";

        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            SendersReference = null, // No senders reference
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = phoneNumber,
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = messageBody,
                    Sender = "TestSender"
                }
            }
        };

        // Act
        var result = request.MapToInstantSmsNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.SendersReference);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(3600)]
    [InlineData(172800)]
    public void MapToInstantSmsNotificationOrder_WithDifferentTimeToLive_MapsCorrectly(int timeToLive)
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;
        var phoneNumber = "+4799999999";
        var messageBody = "Test SMS message";

        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = phoneNumber,
                TimeToLiveInSeconds = timeToLive,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = messageBody,
                    Sender = "TestSender"
                }
            }
        };

        // Act
        var result = request.MapToInstantSmsNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(timeToLive, result.ShortMessageDeliveryDetails.TimeToLiveInSeconds);
    }

    [Fact]
    public void MapToInstantSmsNotificationOrder_GeneratesUniqueIds()
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;

        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = "Test message",
                    Sender = "TestSender"
                }
            }
        };

        // Act
        var result1 = request.MapToInstantSmsNotificationOrder(creatorShortName, created);
        var result2 = request.MapToInstantSmsNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotEqual(result1.OrderId, result2.OrderId);
        Assert.NotEqual(result1.OrderChainId, result2.OrderChainId);
    }

    [Fact]
    public void MapToInstantSmsNotificationOrder_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        InstantSmsNotificationOrderRequestExt? request = null;
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            request!.MapToInstantSmsNotificationOrder(creatorShortName, created));
    }

    [Fact]
    public void MapToInstantSmsNotificationOrder_WithNullRecipientSms_ThrowsArgumentNullException()
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            RecipientSms = null!
        };
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            request.MapToInstantSmsNotificationOrder(creatorShortName, created));
    }

    [Fact]
    public void MapToInstantSmsNotificationOrder_WithNullShortMessageContent_ThrowsArgumentNullException()
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = null!
            }
        };
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            request.MapToInstantSmsNotificationOrder(creatorShortName, created));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MapToInstantSmsNotificationOrder_WithInvalidCreatorShortName_ThrowsArgumentException(string? creatorShortName)
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = "Test message",
                    Sender = "TestSender"
                }
            }
        };
        var created = DateTime.UtcNow;

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            request.MapToInstantSmsNotificationOrder(creatorShortName!, created));
    }

    [Fact]
    public void MapToInstantSmsNotificationOrder_WithNullCreatorShortName_ThrowsArgumentNullException()
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = "Test message",
                    Sender = "TestSender"
                }
            }
        };
        var created = DateTime.UtcNow;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            request.MapToInstantSmsNotificationOrder(null!, created));
    }

    [Fact]
    public void MapToInstantSmsNotificationOrder_WithLongMessage_MapsCorrectly()
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;
        var longMessage = new string('a', 1000); // Very long message

        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = longMessage,
                    Sender = "TestSender"
                }
            }
        };

        // Act
        var result = request.MapToInstantSmsNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(longMessage, result.ShortMessageDeliveryDetails.ShortMessageContent.Message);
    }

    [Theory]
    [InlineData("+4799999999")]
    [InlineData("004799999999")]
    public void MapToInstantSmsNotificationOrder_WithDifferentPhoneNumberFormats_MapsCorrectly(string phoneNumber)
    {
        // Arrange
        var creatorShortName = "ttd";
        var created = DateTime.UtcNow;

        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "test-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = phoneNumber,
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = "Test message",
                    Sender = "TestSender"
                }
            }
        };

        // Act
        var result = request.MapToInstantSmsNotificationOrder(creatorShortName, created);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(phoneNumber, result.ShortMessageDeliveryDetails.PhoneNumber);
    }
}
