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
    public void Validate_WithValidInstantNotificationOrderRequest_ShouldPass()
    {
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = "EB08BC4E-A35A-4B37-841A-9EC5D41E3E14",
            SendersReference = "AE16178F-8C72-4C6B-AE7E-E18D60B7BF94",

            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    TimeToLiveInSeconds = 3600,
                    PhoneNumber = "+4799999999",

                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Sender = "Altinn",
                        Body = "Test message"
                    }
                }
            }
        };

        var validationResult = _validator.Validate(request);

        Assert.True(validationResult.IsValid);
        Assert.Empty(validationResult.Errors);
    }

    [Fact]
    public void Validate_WithInvalidInstantNotificationOrderRequest_ShouldFail()
    {
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = "39A3D898-37D6-4507-A7AE-27FECD63884C",
            SendersReference = "6C176F56-F69E-445B-9631-BAA68F0D5AB3",

            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    TimeToLiveInSeconds = 45, // Invalid: must be between 60 and 172800 seconds
                    PhoneNumber = "invalid-number", // Invalid: not a valid mobile number in an international format

                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Sender = "Altinn",
                        Body = "Test message"
                    }
                }
            }
        };

        var validationResult = _validator.Validate(request);

        Assert.False(validationResult.IsValid);
        Assert.Equal(2, validationResult.Errors.Count);
    }
}
