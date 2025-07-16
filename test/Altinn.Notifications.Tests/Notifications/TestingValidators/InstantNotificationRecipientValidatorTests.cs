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
            IdempotencyId = "id-123",
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    PhoneNumber = "+4712345678",
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

    [Fact]
    public void Validate_WithInvalidRecipient_ReturnsInvalid()
    {
        var request = new InstantNotificationOrderRequestExt
        {
            IdempotencyId = "39A3D898-37D6-4507-A7AE-27FECD63884C",
            SendersReference = "6C176F56-F69E-445B-9631-BAA68F0D5AB3",
            InstantNotificationRecipient = new InstantNotificationRecipientExt
            {
                ShortMessageDeliveryDetails = new ShortMessageDeliveryDetailsExt
                {
                    TimeToLiveInSeconds = 3600,
                    PhoneNumber = "invalid-number",
                    ShortMessageContent = new ShortMessageContentExt
                    {
                        Sender = "Altinn",
                        Body = "Test message"
                    }
                }
            }
        };

        var validationResult = _validator.Validate(request.InstantNotificationRecipient);

        Assert.False(validationResult.IsValid);
    }
}
