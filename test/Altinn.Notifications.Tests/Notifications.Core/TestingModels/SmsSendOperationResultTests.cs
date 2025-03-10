﻿using System;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingModels;

public class SmsSendOperationResultTests
{
    [Fact]
    public void TryParse_EmptyInput_ReturnsFalseAndDefaultResult()
    {
        // Arrange
        string input = string.Empty;

        // Act
        bool parseResult = SmsSendOperationResult.TryParse(input, out SmsSendOperationResult result);

        // Assert
        Assert.NotNull(result);
        Assert.False(parseResult);
        Assert.Null(result.NotificationId);
        Assert.Empty(result.GatewayReference);
        Assert.Equal(SmsNotificationResultType.New, result.SendResult);
    }

    [Fact]
    public void TryParse_WhitespaceInput_ReturnsFalseAndDefaultResult()
    {
        // Arrange
        string input = " ";

        // Act
        bool parseResult = SmsSendOperationResult.TryParse(input, out SmsSendOperationResult result);

        // Assert
        Assert.NotNull(result);
        Assert.False(parseResult);
        Assert.Null(result.NotificationId);
        Assert.Empty(result.GatewayReference);
        Assert.Equal(SmsNotificationResultType.New, result.SendResult);
    }

    [Fact]
    public void TryParse_ValidInput_ReturnsTrueAndPopulatedResult()
    {
        // Arrange
        string input = "{\"notificationId\":\"d3b3f3e3-3e3b-3b3b-3b3b-3b3b3b3b3b3b\",\"gatewayReference\":\"123456789\",\"sendResult\":3}";

        // Act
        bool parseResult = SmsSendOperationResult.TryParse(input, out SmsSendOperationResult result);

        // Assert
        Assert.NotNull(result);
        Assert.True(parseResult);
        Assert.Equal("123456789", result.GatewayReference);
        Assert.Equal(SmsNotificationResultType.Delivered, result.SendResult);
        Assert.Equal(Guid.Parse("d3b3f3e3-3e3b-3b3b-3b3b-3b3b3b3b3b3b"), result.NotificationId);
    }

    [Fact]
    public void TryParse_InvalidJson_ReturnsFalseAndDefaultResult()
    {
        // Arrange
        string input = "{\"invalidJson\":\"value\"}";

        // Act
        bool parseResult = SmsSendOperationResult.TryParse(input, out SmsSendOperationResult result);

        // Assert
        Assert.NotNull(result);
        Assert.False(parseResult);
        Assert.Null(result.NotificationId);
        Assert.Empty(result.GatewayReference);
        Assert.Equal(SmsNotificationResultType.New, result.SendResult);
    }

    [Fact]
    public void TryParse_NullIdWithGatewayReference_ReturnsTrueAndPopulatedResult()
    {
        // Arrange
        string input = "{\"notificationId\":\"00000000-0000-0000-0000-000000000000\",\"gatewayReference\":\"123456789\",\"sendResult\":3}";

        // Act
        bool parseResult = SmsSendOperationResult.TryParse(input, out SmsSendOperationResult result);

        // Assert
        Assert.NotNull(result);
        Assert.True(parseResult);
        Assert.Equal(Guid.Empty, result.NotificationId);
        Assert.Equal("123456789", result.GatewayReference);
        Assert.Equal(SmsNotificationResultType.Delivered, result.SendResult);
    }

    [Fact]
    public void TryParse_MalformedJson_ReturnsFalseAndDefaultResult()
    {
        // Arrange
        string input = "{";

        // Act
        bool parseResult = SmsSendOperationResult.TryParse(input, out SmsSendOperationResult result);

        // Assert
        Assert.NotNull(result);
        Assert.False(parseResult);
        Assert.Null(result.NotificationId);
        Assert.Empty(result.GatewayReference);
        Assert.Equal(SmsNotificationResultType.New, result.SendResult);
    }

    [Fact]
    public void Serialize_ReturnsValidJsonString()
    {
        // Arrange
        var smsSendOperationResult = new SmsSendOperationResult
        {
            GatewayReference = "123456789",
            SendResult = SmsNotificationResultType.Delivered,
            NotificationId = Guid.Parse("d3b3f3e3-3e3b-3b3b-3b3b-3b3b3b3b3b3b")
        };

        // Act
        string jsonString = smsSendOperationResult.Serialize();

        // Assert
        Assert.Contains("\"sendResult\":\"Delivered\"", jsonString);
        Assert.Contains("\"gatewayReference\":\"123456789\"", jsonString);
        Assert.Contains("\"notificationId\":\"d3b3f3e3-3e3b-3b3b-3b3b-3b3b3b3b3b3b\"", jsonString);
    }

    [Fact]
    public void Deserialize_ValidJsonString_ReturnsObject()
    {
        // Arrange
        string jsonString = "{\"notificationId\":\"d3b3f3e3-3e3b-3b3b-3b3b-3b3b3b3b3b3b\",\"gatewayReference\":\"123456789\",\"sendResult\":2}";

        // Act
        var result = SmsSendOperationResult.Deserialize(jsonString);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("123456789", result.GatewayReference);
        Assert.Equal(SmsNotificationResultType.Accepted, result.SendResult);
        Assert.Equal(Guid.Parse("d3b3f3e3-3e3b-3b3b-3b3b-3b3b3b3b3b3b"), result.NotificationId);
    }
}
