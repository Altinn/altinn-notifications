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

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("+47")]
    [InlineData("12345")]
    [InlineData("abcdefg")]
    [InlineData("+479999999")]
    [InlineData("+479999999999")]
    public void Validate_DeliveryDetailsWithInvalidPhoneNumber_ShouldFail(string invalidPhoneNumber)
    {
        // Arrange
        var details = new ShortMessageDeliveryDetailsExt
        {
            TimeToLiveInSeconds = 3600,
            PhoneNumber = invalidPhoneNumber,
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
    }

    [Theory]
    [InlineData(59)]
    [InlineData(172801)]
    public void Validate_DeliveryDetailsWithOutOfRangeTimeToLive_ShouldFail(int timeToLiveInSeconds)
    {
        // Arrange
        var details = new ShortMessageDeliveryDetailsExt
        {
            PhoneNumber = "+4799999999",
            TimeToLiveInSeconds = timeToLiveInSeconds,
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
    }

    [Theory]
    [InlineData("Hello world!", "Altinn", "+4791234567", 600)]
    [InlineData("Short message", "Altinn", "+4799999999", 3600)]
    [InlineData("1234567890", "SenderName", "+4798765432", 172800)]
    [InlineData("Test message", "Test sender", "+4795555555", 120)]
    public void Validate_DeliveryDetailsWithValidContent_ShouldPass(string body, string sender, string phoneNumber, int timeToLiveInSeconds)
    {
        // Arrange
        var details = new ShortMessageDeliveryDetailsExt
        {
            PhoneNumber = phoneNumber,
            TimeToLiveInSeconds = timeToLiveInSeconds,

            ShortMessageContent = new ShortMessageContentExt
            {
                Body = body,
                Sender = sender
            }
        };

        // Act
        var result = _validator.Validate(details);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
