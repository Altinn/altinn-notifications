using Altinn.Notifications.Models.Orders;
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
    public void Validate_WithValidRecipient_ReturnsValid()
    {
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = "A292D9BF-4F54-48C0-BD3E-59F1617FBED5",

            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    PhoneNumber = "+4799999999",
                    TimeToLiveInSeconds = 3600,
                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Body = "Test",
                        Sender = "Altinn"
                    }
                }
            }
        };

        var validationResult = _validator.Validate(request.InstantNotificationRecipient);

        Assert.True(validationResult.IsValid);
    }

    [Theory]
    [InlineData("+4799999999")]
    [InlineData("004799999999")]
    public void Validate_WithDifferentValidPhoneNumberFormats_ReturnsValid(string phoneNumber)
    {
        var recipient = new InstantNotificationRecipientExt
        {
            ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = phoneNumber,
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = "Test",
                    Sender = "Altinn"
                }
            }
        };

        var validationResult = _validator.Validate(recipient);

        Assert.True(validationResult.IsValid);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(-1)]
    [InlineData(31536001)]
    public void Validate_WithInvalidTimeToLiveInSeconds_ReturnsInvalid(int timeToLiveInSeconds)
    {
        var recipient = new InstantNotificationRecipientExt
        {
            ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",

                TimeToLiveInSeconds = timeToLiveInSeconds,

                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = "Test",
                    Sender = "Altinn"
                }
            }
        };

        var validationResult = _validator.Validate(recipient);

        Assert.False(validationResult.IsValid);
    }
}
