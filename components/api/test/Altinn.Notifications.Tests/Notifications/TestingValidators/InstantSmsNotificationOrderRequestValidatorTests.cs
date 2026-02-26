using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators.Orders;
using FluentValidation.TestHelper;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

/// <summary>
/// Tests for <see cref="InstantSmsNotificationOrderRequestValidator"/>
/// </summary>
public class InstantSmsNotificationOrderRequestValidatorTests
{
    private readonly InstantSmsNotificationOrderRequestValidator _validator;

    public InstantSmsNotificationOrderRequestValidatorTests()
    {
        _validator = new InstantSmsNotificationOrderRequestValidator();
    }

    [Fact]
    public void Validate_ValidInstantSmsNotificationOrderRequest_NoValidationErrors()
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            SendersReference = "valid-senders-reference",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = "Valid SMS message body",
                    Sender = "ValidSender"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyOrNullIdempotencyId_HasValidationError(string? idempotencyId)
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = idempotencyId!,
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = "Valid SMS message body",
                    Sender = "ValidSender"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.IdempotencyId);
    }

    [Fact]
    public void Validate_NullRecipientSms_HasValidationError()
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            RecipientSms = null!
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RecipientSms);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("+4712345678")] // Invalid Norwegian mobile number
    [InlineData("1234567890")] // Missing country code
    [InlineData("invalid-phone")]
    public void Validate_InvalidPhoneNumber_HasValidationError(string? phoneNumber)
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = phoneNumber!,
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = "Valid SMS message body",
                    Sender = "ValidSender"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RecipientSms.PhoneNumber);
    }

    [Theory]
    [InlineData(59)] // Below minimum
    [InlineData(172801)] // Above maximum
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidTimeToLiveInSeconds_HasValidationError(int timeToLiveInSeconds)
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = timeToLiveInSeconds,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = "Valid SMS message body",
                    Sender = "ValidSender"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RecipientSms.TimeToLiveInSeconds);
    }

    [Theory]
    [InlineData(60)] // Minimum valid
    [InlineData(3600)] // Common value
    [InlineData(172800)] // Maximum valid
    public void Validate_ValidTimeToLiveInSeconds_NoValidationError(int timeToLiveInSeconds)
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = timeToLiveInSeconds,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = "Valid SMS message body",
                    Sender = "ValidSender"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.RecipientSms.TimeToLiveInSeconds);
    }

    [Fact]
    public void Validate_NullShortMessageContent_NoValidationError()
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = null!
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.RecipientSms.ShortMessageContent);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyOrNullMessageBody_NoValidationError(string? messageBody)
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = messageBody!,
                    Sender = "ValidSender"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.RecipientSms.ShortMessageContent.Body);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ValidSender")]
    public void Validate_NullEmptyOrValidSender_NoValidationError(string? sender)
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = "Valid SMS message body",
                    Sender = sender
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.RecipientSms.ShortMessageContent.Sender);
    }

    [Fact]
    public void Validate_LongMessageBody_NoValidationError()
    {
        // Arrange
        var longMessage = new string('a', 2000); // Very long message
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = longMessage,
                    Sender = "ValidSender"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.RecipientSms.ShortMessageContent.Body);
    }

    [Theory]
    [InlineData("+4799999999")] // Norwegian mobile
    [InlineData("004799999999")] // International prefix
    public void Validate_ValidPhoneNumberFormats_NoValidationError(string phoneNumber)
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = phoneNumber,
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = "Valid SMS message body",
                    Sender = "ValidSender"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.RecipientSms.PhoneNumber);
    }

    [Fact]
    public void Validate_OptionalSendersReference_NoValidationError()
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            SendersReference = null, // Optional field
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = "Valid SMS message body",
                    Sender = "ValidSender"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.SendersReference);
    }

    [Fact]
    public void Validate_WithSpecialCharactersInMessage_NoValidationError()
    {
        // Arrange
        var messageWithSpecialChars = "Message with æøå and special chars: !@#$%^&*()";
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = messageWithSpecialChars,
                    Sender = "ValidSender"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.RecipientSms.ShortMessageContent.Body);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Validate_WhitespaceOnlyMessageBody_NoValidationError(string whitespaceMessage)
    {
        // Arrange
        var request = new InstantSmsNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            RecipientSms = new ShortMessageDeliveryDetailsExt
            {
                PhoneNumber = "+4799999999",
                TimeToLiveInSeconds = 3600,
                ShortMessageContent = new ShortMessageContentExt
                {
                    Body = whitespaceMessage,
                    Sender = "ValidSender"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.RecipientSms.ShortMessageContent.Body);
    }
}
