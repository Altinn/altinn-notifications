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
            string input = "{\"notificationId\":\"d3b3f3e3-3e3b-3b3b-3b3b-3b3b3b3b3b3b\",\"gatewayReference\":\"123456789\",\"sendResult\":1}";

            // Act
            bool result = SmsSendOperationResult.TryParse(input, out SmsSendOperationResult value);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void TryParse_ValidInput_NoNotificationId_ReturnsTrue()
        {
            // Arrange
            string input = "{\"gatewayReference\":\"123456789\",\"sendResult\":1}";

            // Act
            bool result = SmsSendOperationResult.TryParse(input, out SmsSendOperationResult value);

            // Assert
            Assert.True(result);
        }
    }
}
