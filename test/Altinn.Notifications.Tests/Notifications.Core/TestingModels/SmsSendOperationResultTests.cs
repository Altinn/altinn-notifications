using System;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingModels;

public class SmsSendOperationResultTests
{
    [Fact]
    public void TryParse_EmptyInput_ReturnsFalse()
    {
        // Arrange
        string input = string.Empty;

        // Act
        bool parseResult = SmsSendOperationResult.TryParse(input, out SmsSendOperationResult result);

        // Assert
        Assert.NotNull(result);
        Assert.False(parseResult);
        Assert.Equal(Guid.Empty, result.Id);
        Assert.Null(result.GatewayReference);
        Assert.Equal(SmsNotificationResultType.New, result.SendResult);
    }

    [Fact]
    public void TryParse_WhitespaceInput_ReturnsFalse()
    {
        // Arrange
        string input = " ";

        // Act
        bool parseResult = SmsSendOperationResult.TryParse(input, out SmsSendOperationResult result);

        // Assert
        Assert.NotNull(result);
        Assert.False(parseResult);
        Assert.Equal(Guid.Empty, result.Id);
        Assert.Null(result.GatewayReference);
        Assert.Equal(SmsNotificationResultType.New, result.SendResult);
    }

    [Fact]
    public void TryParse_ValidInput_ReturnsTrue()
    {
        // Arrange
        string input = "{\"id\":\"d3b3f3e3-3e3b-3b3b-3b3b-3b3b3b3b3b3b\",\"gatewayReference\":\"123456789\",\"sendResult\":3}";

        // Act
        bool parseResult = SmsSendOperationResult.TryParse(input, out SmsSendOperationResult result);

        // Assert
        Assert.NotNull(result);
        Assert.True(parseResult);
        Assert.Equal("123456789", result.GatewayReference);
        Assert.Equal(SmsNotificationResultType.Delivered, result.SendResult);
        Assert.Equal(Guid.Parse("d3b3f3e3-3e3b-3b3b-3b3b-3b3b3b3b3b3b"), result.Id);
    }

    [Fact]
    public void TryParse_InvalidJson_ReturnsFalse()
    {
        // Arrange
        string input = "{\"invalidJson\":\"value\"}";

        // Act
        bool parseResult = SmsSendOperationResult.TryParse(input, out SmsSendOperationResult result);

        // Assert
        Assert.NotNull(result);
        Assert.False(parseResult);
        Assert.Equal(Guid.Empty, result.Id);
        Assert.Null(result.GatewayReference);
        Assert.Equal(SmsNotificationResultType.New, result.SendResult);
    }

    [Fact]
    public void TryParse_NullIdWithGatewayReference_ReturnsFalse()
    {
        // Arrange
        string input = "{\"id\":\"00000000-0000-0000-0000-000000000000\",\"gatewayReference\":\"123456789\",\"sendResult\":3}";

        // Act
        bool parseResult = SmsSendOperationResult.TryParse(input, out SmsSendOperationResult result);

        // Assert
        Assert.NotNull(result);
        Assert.False(parseResult);
        Assert.Equal(Guid.Empty, result.Id);
        Assert.Equal("123456789", result.GatewayReference);
        Assert.Equal(SmsNotificationResultType.Delivered, result.SendResult);
    }

    [Fact]
    public void TryParse_MalformedJson_ReturnsFalse()
    {
        // Arrange
        string input = "{";

        // Act
        bool parseResult = SmsSendOperationResult.TryParse(input, out SmsSendOperationResult result);

        // Assert
        // Exception is caught, so TryParse returns false and result is a new instance with default values.
        Assert.NotNull(result);
        Assert.False(parseResult);
        Assert.Equal(Guid.Empty, result.Id);
        Assert.Null(result.GatewayReference);
        Assert.Equal(SmsNotificationResultType.New, result.SendResult);
    }
}
