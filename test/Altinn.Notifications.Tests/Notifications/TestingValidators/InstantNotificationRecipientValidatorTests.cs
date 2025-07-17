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

    [Theory]
    [InlineData("A292D9BF-4F54-48C0-BD3E-59F1617FBED5", "+4799999999", 3600, "Test", "Altinn")]
    [InlineData("B1234567-89AB-CDEF-0123-456789ABCDEF", "004799999999", 1800, "Hello", "Test sender")]
    public void Validate_OrderRequestWithValidInformation_ShouldPass(string idempotencyId, string phoneNumber, int timeToLiveInSeconds, string body, string sender)
    {
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = idempotencyId,

            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    PhoneNumber = phoneNumber,
                    TimeToLiveInSeconds = timeToLiveInSeconds,

                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Body = body,
                        Sender = sender
                    }
                }
            }
        };

        var validationResult = _validator.Validate(request.InstantNotificationRecipient);

        Assert.True(validationResult.IsValid);
        Assert.Empty(validationResult.Errors);
    }

    [Theory]
    [InlineData("A292D9BF-4F54-48C0-BD3E-59F1617FBED5", "+4799999999", -1, "Test", "Altinn")]
    [InlineData("A292D9BF-4F54-48C0-BD3E-59F1617FBED5", "+4712345678", 3600, "Test", "Altinn")]
    public void Validate_OrderRequestWithInvalidInformation_ShouldFail(string idempotencyId, string phoneNumber, int timeToLiveInSeconds, string body, string sender)
    {
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = idempotencyId,

            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    PhoneNumber = phoneNumber,
                    TimeToLiveInSeconds = timeToLiveInSeconds,

                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Body = body,
                        Sender = sender
                    }
                }
            }
        };

        var validationResult = _validator.Validate(request.InstantNotificationRecipient);

        Assert.False(validationResult.IsValid);
        Assert.Single(validationResult.Errors);
    }
}
