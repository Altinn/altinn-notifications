using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators.Sms;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class ShortMessageDeliveryDetailsValidatorTests
{
    private readonly ShortMessageDeliveryDetailsValidator _validator;

    public ShortMessageDeliveryDetailsValidatorTests()
    {
        _validator = new ShortMessageDeliveryDetailsValidator();
    }

    [Fact]
    public void Validate_WithInvalidPhoneNumber_ShouldFail()
    {
        // Arrange
        var details = new ShortMessageDeliveryDetailsExt
        {
            PhoneNumber = "12345",
            TimeToLiveInSeconds = 3600,
            ShortMessageContent = new ShortMessageContentExt
            {
                Body = "Test message",
                Sender = "Test sender"
            }
        };

        // Act
        var result = _validator.Validate(details);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("Recipient phone number is not a valid mobile number.", result.Errors[0].ErrorMessage);
    }

    [Fact]
    public void Validate_WithTimeToLiveOutOfRange_ShouldFail()
    {
        // Arrange
        var details = new ShortMessageDeliveryDetailsExt
        {
            TimeToLiveInSeconds = 30,
            PhoneNumber = "+4799999999",
            ShortMessageContent = new ShortMessageContentExt
            {
                Body = "Test message",
                Sender = "Test sender"
            }
        };

        // Act
        var result = _validator.Validate(details);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("Time-to-live must be between 60 and 172800 seconds (48 hours).", result.Errors[0].ErrorMessage);
    }

    [Fact]
    public void Validate_WithNullShortMessageContent_ShouldFail()
    {
        // Arrange
        var details = new ShortMessageDeliveryDetailsExt
        {
            PhoneNumber = "+4799999999",
            TimeToLiveInSeconds = 3600,
            ShortMessageContent = null
        };

        // Act
        var result = _validator.Validate(details);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("SMS details cannot be null.", result.Errors[0].ErrorMessage);
    }

    [Fact]
    public void Validate_WithValidShortMessageDeliveryDetailsExt_ShouldPass()
    {
        // Arrange
        var details = new ShortMessageDeliveryDetailsExt
        {
            TimeToLiveInSeconds = 3600,
            PhoneNumber = "+4799999999",
            ShortMessageContent = new ShortMessageContentExt
            {
                Body = "Test message",
                Sender = "Test sender"
            }
        };

        // Act
        var result = _validator.Validate(details);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
