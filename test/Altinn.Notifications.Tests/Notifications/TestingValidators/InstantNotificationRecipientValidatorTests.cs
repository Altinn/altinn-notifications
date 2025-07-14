using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators.Recipient;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class InstantNotificationRecipientValidatorTests
{
    private readonly InstantNotificationRecipientValidator _validator;

    public InstantNotificationRecipientValidatorTests()
    {
        _validator = new InstantNotificationRecipientValidator();
    }

    [Fact]
    public void Validate_WithNullShortMessageDeliveryDetails_ShouldFail()
    {
        // Arrange
        var recipient = new InstantNotificationRecipientExt
        {
            ShortMessageDeliveryDetails = null
        };

        // Act
        var result = _validator.Validate(recipient);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("SMS delivery details cannot be null.", result.Errors[0].ErrorMessage);
    }

    [Fact]
    public void Validate_WithInvalidShortMessageDeliveryDetails_ShouldFail()
    {
        // Arrange
        var recipient = new InstantNotificationRecipientExt
        {
            ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "12345",
                TimeToLiveInSeconds = 30,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = "Test message",
                    Sender = "Test sender"
                }
            }
        };

        // Act
        var result = _validator.Validate(recipient);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Recipient phone number is not a valid mobile number.");
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Time-to-live must be between 60 and 172800 seconds (48 hours).");
    }

    [Fact]
    public void Validate_ValidRecipient_ShouldPass()
    {
        // Arrange
        var recipient = new InstantNotificationRecipientExt
        {
            ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = "Test message",
                    Sender = "Test sender"
                }
            }
        };

        // Act
        var result = _validator.Validate(recipient);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
