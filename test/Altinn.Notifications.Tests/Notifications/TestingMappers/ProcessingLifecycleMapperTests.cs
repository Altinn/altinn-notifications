using System;
using Altinn.Notifications.Persistence.Mappers;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class ProcessingLifecycleMapperTests
{
    [Fact]
    public void GetSmsLifecycleStage_WithInvalidStatus_ThrowsException()
    {
        // Arrange
        string invalidStatus = "InvalidStatus";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ProcessingLifecycleMapper.GetSmsLifecycleStage(invalidStatus));
    }

    [Fact]
    public void GetEmailLifecycleStage_WithInvalidStatus_ThrowsException()
    {
        // Arrange
        string invalidStatus = "InvalidStatus";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ProcessingLifecycleMapper.GetEmailLifecycleStage(invalidStatus));
    }

    [Fact]
    public void GetOrderLifecycleStage_WithInvalidStatus_ThrowsException()
    {
        // Arrange
        string invalidStatus = "InvalidStatus";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ProcessingLifecycleMapper.GetOrderLifecycleStage(invalidStatus));
    }
}
