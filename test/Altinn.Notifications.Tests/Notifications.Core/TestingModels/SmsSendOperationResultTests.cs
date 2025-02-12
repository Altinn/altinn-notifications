using System;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingModels
{
    public class SmsSendOperationResultTests
    {
        [Fact]
        public void TryParse_ValidInput_ReturnsTrue()
        {
            // Arrange
            string input = "{\"Id\":\"d3b3f3e3-3e3b-3b3b-3b3b-3b3b3b3b3b3b\",\"GatewayReference\":\"123456789\",\"SendResult\":3}";

            // Act
            bool result = SmsSendOperationResult.TryParse(input, out var parsedResult);

            // Assert
            Assert.True(result);
            Assert.NotNull(parsedResult);
            Assert.Equal("123456789", parsedResult.GatewayReference);
            Assert.Equal(SmsNotificationResultType.Delivered, parsedResult.SendResult);
            Assert.Equal(Guid.Parse("d3b3f3e3-3e3b-3b3b-3b3b-3b3b3b3b3b3b"), parsedResult.Id);
        }

        [Fact]
        public void TryParse_InvalidInput_ReturnsFalse()
        {
            // Arrange
            string input = "{\"InvalidJson\":\"value\"}";

            // Act
            bool result = SmsSendOperationResult.TryParse(input, out var parsedResult);

            // Assert
            Assert.False(result);
            Assert.NotNull(parsedResult);
            Assert.Equal(Guid.Empty, parsedResult.Id);
            Assert.Null(parsedResult.GatewayReference);
            Assert.Equal(SmsNotificationResultType.New, parsedResult.SendResult);
        }

        [Fact]
        public void TryParse_EmptyInput_ReturnsFalse()
        {
            // Arrange
            string input = string.Empty;

            // Act
            bool result = SmsSendOperationResult.TryParse(input, out var parsedResult);

            // Assert
            Assert.False(result);
            Assert.NotNull(parsedResult);
            Assert.Equal(Guid.Empty, parsedResult.Id);
            Assert.Null(parsedResult.GatewayReference);
            Assert.Equal(SmsNotificationResultType.New, parsedResult.SendResult);
        }

        [Fact]
        public void TryParse_NullIdAndEmptyGatewayReference_ReturnsFalse()
        {
            // Arrange
            string input = "{\"Id\":\"00000000-0000-0000-0000-000000000000\",\"GatewayReference\":\"\",\"SendResult\":3}";

            // Act
            bool result = SmsSendOperationResult.TryParse(input, out var parsedResult);

            // Assert
            Assert.False(result);
            Assert.NotNull(parsedResult);
            Assert.Equal(Guid.Empty, parsedResult.Id);
            Assert.Equal(string.Empty, parsedResult.GatewayReference);
            Assert.Equal(SmsNotificationResultType.Delivered, parsedResult.SendResult);
        }

        [Fact]
        public void Serialize_ReturnsValidJsonString()
        {
            // Arrange
            var smsSendOperationResult = new SmsSendOperationResult
            {
                GatewayReference = "123456789",
                SendResult = SmsNotificationResultType.Delivered,
                Id = Guid.Parse("d3b3f3e3-3e3b-3b3b-3b3b-3b3b3b3b3b3b")
            };

            // Act
            string jsonString = smsSendOperationResult.Serialize();

            // Assert
            Assert.Contains("\"sendResult\":\"Delivered\"", jsonString);
            Assert.Contains("\"gatewayReference\":\"123456789\"", jsonString);
            Assert.Contains("\"id\":\"d3b3f3e3-3e3b-3b3b-3b3b-3b3b3b3b3b3b\"", jsonString);
        }

        [Fact]
        public void Deserialize_ValidJsonString_ReturnsObject()
        {
            // Arrange
            string jsonString = "{\"Id\":\"d3b3f3e3-3e3b-3b3b-3b3b-3b3b3b3b3b3b\",\"GatewayReference\":\"123456789\",\"SendResult\":2}";

            // Act
            var result = SmsSendOperationResult.Deserialize(jsonString);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("123456789", result.GatewayReference);
            Assert.Equal(SmsNotificationResultType.Accepted, result.SendResult);
            Assert.Equal(Guid.Parse("d3b3f3e3-3e3b-3b3b-3b3b-3b3b3b3b3b3b"), result.Id);
        }
    }
}
