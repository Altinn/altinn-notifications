using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators.Orders;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class InstantNotificationOrderRequestValidatorTests
{
    private readonly InstantNotificationOrderRequestValidator _validator;

    public InstantNotificationOrderRequestValidatorTests()
    {
        _validator = new InstantNotificationOrderRequestValidator();
    }

    [Fact]
    public void Validate_WithNullRecipient_ShouldFail()
    {
        // Arrange
        var request = new InstantNotificationOrderRequestExt
        {
            InstantNotificationRecipient = null,
            IdempotencyId = "DE3CE46D-CE34-48C5-A58E-DE53CDA3475F"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("Recipient information cannot be null.", result.Errors[0].ErrorMessage);
    }

    [Fact]
    public void Validate_WithInvalidRecipient_ShouldFail()
    {
        // Arrange
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = "64777517-EA4D-4FD8-BC9F-A519D0B1AC26",
            InstantNotificationRecipient = new InstantNotificationRecipientExt
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
            }
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Recipient phone number is not a valid mobile number.");
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Time-to-live must be between 60 and 172800 seconds (48 hours).");
    }

    [Fact]
    public void Validate_WithValidInstantNotificationOrderRequestExt_ShouldPass()
    {
        // Arrange
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = "CE31403B-4429-47CD-9AB3-A4A40F7DE0BE",
            InstantNotificationRecipient = new InstantNotificationRecipientExt
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
            }
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
