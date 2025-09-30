using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Validators.Orders;
using FluentValidation.TestHelper;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

/// <summary>
/// Tests for <see cref="InstantEmailNotificationOrderRequestValidator"/>
/// </summary>
public class InstantEmailNotificationOrderRequestValidatorTests
{
    private readonly InstantEmailNotificationOrderRequestValidator _validator;

    public InstantEmailNotificationOrderRequestValidatorTests()
    {
        _validator = new InstantEmailNotificationOrderRequestValidator();
    }

    [Fact]
    public void Validate_ValidInstantEmailNotificationOrderRequest_NoValidationErrors()
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            SendersReference = "valid-senders-reference",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Valid Email Subject",
                    Body = "Valid email body content",
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
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
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = idempotencyId!,
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Valid Subject",
                    Body = "Valid body",
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.IdempotencyId);
    }

    [Fact]
    public void Validate_NullInstantEmailDetails_NoValidationError()
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            InstantEmailDetails = null!
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.InstantEmailDetails);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("invalid-email")]
    [InlineData("@example.com")]
    [InlineData("test@")]
    [InlineData("test.example.com")]
    public void Validate_InvalidEmailAddress_HasValidationError(string? emailAddress)
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = emailAddress!,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Valid Subject",
                    Body = "Valid body",
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.InstantEmailDetails.EmailAddress);
    }

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name+tag@domain.co.uk")]
    [InlineData("simple@test.org")]
    [InlineData("user123@example-domain.com")]
    public void Validate_ValidEmailAddresses_NoValidationError(string emailAddress)
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = emailAddress,
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Valid Subject",
                    Body = "Valid body",
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.InstantEmailDetails.EmailAddress);
    }

    [Fact]
    public void Validate_NullEmailSettings_HasValidationError()
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = null!
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.InstantEmailDetails.EmailSettings);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyOrNullSubject_HasValidationError(string? subject)
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = subject!,
                    Body = "Valid body",
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.InstantEmailDetails.EmailSettings.Subject);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyOrNullBody_HasValidationError(string? body)
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Valid Subject",
                    Body = body!,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.InstantEmailDetails.EmailSettings.Body);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("valid@sender.com")]
    public void Validate_NullOrValidSenderEmailAddress_NoValidationError(string? senderEmailAddress)
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Valid Subject",
                    Body = "Valid body",
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = senderEmailAddress
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.InstantEmailDetails.EmailSettings.SenderEmailAddress);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    [InlineData("@example.com")]
    [InlineData("sender@")]
    [InlineData("not.an.email")]
    public void Validate_InvalidSenderEmailAddress_HasValidationError(string senderEmailAddress)
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Valid Subject",
                    Body = "Valid body",
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = senderEmailAddress
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.InstantEmailDetails.EmailSettings.SenderEmailAddress);
    }

    [Theory]
    [InlineData(EmailContentTypeExt.Plain)]
    [InlineData(EmailContentTypeExt.Html)]
    public void Validate_ValidContentTypes_NoValidationError(EmailContentTypeExt contentType)
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Valid Subject",
                    Body = "Valid body",
                    ContentType = contentType,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.InstantEmailDetails.EmailSettings.ContentType);
    }

    [Fact]
    public void Validate_LongSubjectAndBody_NoValidationError()
    {
        // Arrange
        var longSubject = new string('S', 500);
        var longBody = new string('B', 5000);

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = longSubject,
                    Body = longBody,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.InstantEmailDetails.EmailSettings.Subject);
        result.ShouldNotHaveValidationErrorFor(x => x.InstantEmailDetails.EmailSettings.Body);
    }

    [Fact]
    public void Validate_OptionalSendersReference_NoValidationError()
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            SendersReference = null, // Optional field
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Valid Subject",
                    Body = "Valid body",
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.SendersReference);
    }

    [Fact]
    public void Validate_HtmlContentWithHtmlType_NoValidationError()
    {
        // Arrange
        var htmlBody = "<html><body><h1>Test</h1><p>HTML content with <strong>formatting</strong></p></body></html>";

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "HTML Email Test",
                    Body = htmlBody,
                    ContentType = EmailContentTypeExt.Html,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithSpecialCharactersInContent_NoValidationError()
    {
        // Arrange
        var specialSubject = "Test with æøå & special chars! 🎉";
        var specialBody = "Content with æøå, special chars: <>&\"' and emojis 🚀✨";

        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = specialSubject,
                    Body = specialBody,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.InstantEmailDetails.EmailSettings.Subject);
        result.ShouldNotHaveValidationErrorFor(x => x.InstantEmailDetails.EmailSettings.Body);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Validate_WhitespaceOnlySubject_HasValidationError(string whitespaceSubject)
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = whitespaceSubject,
                    Body = "Valid body",
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.InstantEmailDetails.EmailSettings.Subject);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Validate_WhitespaceOnlyBody_HasValidationError(string whitespaceBody)
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Valid Subject",
                    Body = whitespaceBody,
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = "sender@altinn.no"
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.InstantEmailDetails.EmailSettings.Body);
    }

    [Theory]
    [InlineData("   sender@altinn.no   ")]
    [InlineData("\tsender@altinn.no\t")]
    public void Validate_SenderEmailAddressWithWhitespace_NoValidationError(string senderWithWhitespace)
    {
        // Arrange
        var request = new InstantEmailNotificationOrderRequestExt
        {
            IdempotencyId = "valid-idempotency-id",
            InstantEmailDetails = new InstantEmailDetailsExt
            {
                EmailAddress = "test@example.com",
                EmailSettings = new InstantEmailContentExt
                {
                    Subject = "Valid Subject",
                    Body = "Valid body",
                    ContentType = EmailContentTypeExt.Plain,
                    SenderEmailAddress = senderWithWhitespace
                }
            }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.InstantEmailDetails.EmailSettings.SenderEmailAddress);
    }
}
