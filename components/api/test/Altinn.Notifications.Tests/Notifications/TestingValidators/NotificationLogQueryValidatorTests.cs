using Altinn.Notifications.Models.NotificationLog;
using Altinn.Notifications.Validators.Log;

using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class NotificationLogQueryValidatorTests
{
    private readonly NotificationLogQueryValidator _sut = new();

    [Fact]
    public void Validate_WithBothIdentifiersNull_FailsWithExpectedMessage()
    {
        // Arrange
        var request = new NotificationLogQueryExt
        {
            DialogId = null,
            TransmissionId = null
        };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "query");
        Assert.Contains(result.Errors, e => e.ErrorMessage == "At least one of 'dialogId' or 'transmissionId' must be provided.");
    }

    [Fact]
    public void Validate_WithBothIdentifiersWhitespace_FailsWithExpectedMessage()
    {
        // Arrange
        var request = new NotificationLogQueryExt
        {
            DialogId = "   ",
            TransmissionId = "   "
        };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "query");
        Assert.Contains(result.Errors, e => e.ErrorMessage == "At least one of 'dialogId' or 'transmissionId' must be provided.");
    }

    [Fact]
    public void Validate_WithBothIdentifiersEmptyString_FailsWithExpectedMessage()
    {
        // Arrange
        var request = new NotificationLogQueryExt
        {
            DialogId = string.Empty,
            TransmissionId = string.Empty
        };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "query");
        Assert.Contains(result.Errors, e => e.ErrorMessage == "At least one of 'dialogId' or 'transmissionId' must be provided.");
    }

    [Fact]
    public void Validate_WithDialogIdOnly_PassesValidation()
    {
        // Arrange
        var request = new NotificationLogQueryExt
        {
            DialogId = "dialog-123",
            TransmissionId = null
        };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithTransmissionIdOnly_PassesValidation()
    {
        // Arrange
        var request = new NotificationLogQueryExt
        {
            DialogId = null,
            TransmissionId = "transmission-456"
        };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithBothIdentifiersProvided_PassesValidation()
    {
        // Arrange
        var request = new NotificationLogQueryExt
        {
            DialogId = "dialog-123",
            TransmissionId = "transmission-456"
        };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        Assert.True(result.IsValid);
    }
}
