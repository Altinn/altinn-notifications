using System;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Persistence.Mappers;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingMappers;

public class ProcessingLifecycleMapperTests
{
    [Theory]
    [InlineData("delivered", ProcessingLifecycle.SMS_Delivered)]
    [InlineData("accepted", ProcessingLifecycle.SMS_Accepted)]
    [InlineData("failed", ProcessingLifecycle.SMS_Failed)]
    public void GetSmsLifecycleStage_WithValidStatus_ReturnsExpectedEnum(string status, ProcessingLifecycle expected)
    {
        // Act
        var result = ProcessingLifecycleMapper.GetSmsLifecycleStage(status);

        // Assert
        Assert.Equal(expected, result);
    }

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
