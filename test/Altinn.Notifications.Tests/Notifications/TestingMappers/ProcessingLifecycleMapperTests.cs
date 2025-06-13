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

    [Theory]
    [InlineData("delivered", ProcessingLifecycle.Email_Delivered)]
    [InlineData("failed_bounced", ProcessingLifecycle.Email_Failed_Bounced)]
    [InlineData("failed", ProcessingLifecycle.Email_Failed)]
    [InlineData("succeeded", ProcessingLifecycle.Email_Succeeded)]
    [InlineData("sending", ProcessingLifecycle.Email_Sending)]
    public void GetEmailLifecycleStage_WithValidStatus_ReturnsExpectedEnum(string status, ProcessingLifecycle expected)
    {
        // Act
        var result = ProcessingLifecycleMapper.GetEmailLifecycleStage(status);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("cancelled",    ProcessingLifecycle.Order_Cancelled)]
    [InlineData("completed",    ProcessingLifecycle.Order_Completed)]
    [InlineData("processed",    ProcessingLifecycle.Order_Processed)]
    [InlineData("registered",   ProcessingLifecycle.Order_Registered)]
    [InlineData("processing",   ProcessingLifecycle.Order_Processing)]
    [InlineData("sendconditionnotmet", ProcessingLifecycle.Order_SendConditionNotMet)]
    public void GetOrderLifecycleStage_WithValidStatus_ReturnsExpectedEnum(string status, ProcessingLifecycle expected)
    {
        // Act
        var result = ProcessingLifecycleMapper.GetOrderLifecycleStage(status);

        // Assert
        Assert.Equal(expected, result);
    }
}
