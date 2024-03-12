using Altinn.Notifications.Core.Models.Notification;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.Core.TestingModels;

public class EmailSendOperationResultTests
{
    [Fact]
    public void TryParse_ValidInput_ReturnsTrue()
    {
        // Arrange
        string input = "{\"notificationId\":\"d3b3f3e3-3e3b-3b3b-3b3b-3b3b3b3b3b3b\",\"operationId\":\"f2ccdfdd-ed8d-4865-908e-737711496b2b\",\"sendResult\":\"Delivered\"}";

        // Act
        bool result = EmailSendOperationResult.TryParse(input, out _);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TryParse_ValidInput_NoNotificationId_ReturnsTrue()
    {
        // Arrange
        string input = "{\"operationId\":\"f2ccdfdd-ed8d-4865-908e-737711496b2b\",\"sendResult\":\"Delivered\"}";

        // Act
        bool result = EmailSendOperationResult.TryParse(input, out _);

        // Assert
        Assert.True(result);
    }
}
