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
    [InlineData("new", ProcessingLifecycle.Email_New)]
    [InlineData("failed", ProcessingLifecycle.Email_Failed)]
    [InlineData("sending", ProcessingLifecycle.Email_Sending)]
    [InlineData("succeeded", ProcessingLifecycle.Email_Succeeded)]
    [InlineData("delivered", ProcessingLifecycle.Email_Delivered)]
    [InlineData("failed_ttl", ProcessingLifecycle.Email_Failed_TTL)]
    [InlineData("failed_bounced", ProcessingLifecycle.Email_Failed_Bounced)]
    [InlineData("failed_quarantined", ProcessingLifecycle.Email_Failed_Quarantined)]
    [InlineData("failed_filteredspam", ProcessingLifecycle.Email_Failed_FilteredSpam)]
    [InlineData("failed_invalidsasurl", ProcessingLifecycle.Email_Failed_InvalidSasUrl)]
    [InlineData("failed_transienterror", ProcessingLifecycle.Email_Failed_TransientError)]
    [InlineData("failed_payloadtoolarge", ProcessingLifecycle.Email_Failed_PayloadTooLarge)]
    [InlineData("failed_invalidemailformat", ProcessingLifecycle.Email_Failed_InvalidFormat)]
    [InlineData("failed_recipientreserved", ProcessingLifecycle.Email_Failed_RecipientReserved)]
    [InlineData("failed_supressedrecipient", ProcessingLifecycle.Email_Failed_SuppressedRecipient)]
    [InlineData("failed_recipientnotidentified", ProcessingLifecycle.Email_Failed_RecipientNotIdentified)]
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
