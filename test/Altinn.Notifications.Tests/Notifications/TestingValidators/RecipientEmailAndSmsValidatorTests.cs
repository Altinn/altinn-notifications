using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators.Recipient;

using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class RecipientEmailAndSmsValidatorTests
{
    [Fact]
    public void Validate_ValidRecipient_ShouldNotHaveErrors()
    {
        // Arrange
        var validator = new RecipientEmailAndSmsValidator();
        var recipient = new RecipientEmailAndSmsExt
        {
            EmailAddress = "test@example.com",
            PhoneNumber = "+4799999999",
            EmailSettings = new EmailSendingOptionsExt
            {
                Subject = "Test Subject",
                Body = "Test Body",
                ContentType = EmailContentTypeExt.Plain
            },
            SmsSettings = new SmsSendingOptionsExt
            {
                Body = "Test SMS"
            }
        };

        // Act & Assert
        validator.TestValidate(recipient).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_MissingEmailAddress_ShouldHaveError()
    {
        // Arrange
        var validator = new RecipientEmailAndSmsValidator();
        var recipient = new RecipientEmailAndSmsExt
        {
            EmailAddress = string.Empty,
            PhoneNumber = "+4799999999",
            EmailSettings = new EmailSendingOptionsExt
            {
                Subject = "Test Subject",
                Body = "Test Body",
                ContentType = EmailContentTypeExt.Plain
            },
            SmsSettings = new SmsSendingOptionsExt
            {
                Body = "Test SMS"
            }
        };

        // Act & Assert
        validator.TestValidate(recipient)
            .ShouldHaveValidationErrorFor(r => r.EmailAddress);
    }

    [Fact]
    public void Validate_InvalidEmailAddress_ShouldHaveError()
    {
        // Arrange
        var validator = new RecipientEmailAndSmsValidator();
        var recipient = new RecipientEmailAndSmsExt
        {
            EmailAddress = "invalid-email",
            PhoneNumber = "+4799999999",
            EmailSettings = new EmailSendingOptionsExt
            {
                Subject = "Test Subject",
                Body = "Test Body",
                ContentType = EmailContentTypeExt.Plain
            },
            SmsSettings = new SmsSendingOptionsExt
            {
                Body = "Test SMS"
            }
        };

        // Act & Assert
        validator.TestValidate(recipient)
            .ShouldHaveValidationErrorFor(r => r.EmailAddress);
    }

    [Fact]
    public void Validate_MissingPhoneNumber_ShouldHaveError()
    {
        // Arrange
        var validator = new RecipientEmailAndSmsValidator();
        var recipient = new RecipientEmailAndSmsExt
        {
            EmailAddress = "test@example.com",
            PhoneNumber = string.Empty,
            EmailSettings = new EmailSendingOptionsExt
            {
                Subject = "Test Subject",
                Body = "Test Body",
                ContentType = EmailContentTypeExt.Plain
            },
            SmsSettings = new SmsSendingOptionsExt
            {
                Body = "Test SMS"
            }
        };

        // Act & Assert
        validator.TestValidate(recipient)
            .ShouldHaveValidationErrorFor(r => r.PhoneNumber);
    }

    [Fact]
    public void Validate_MissingEmailSettings_ShouldHaveError()
    {
        // Arrange
        var validator = new RecipientEmailAndSmsValidator();
        var recipient = new RecipientEmailAndSmsExt
        {
            EmailAddress = "test@example.com",
            PhoneNumber = "+4799999999",
            EmailSettings = null!,
            SmsSettings = new SmsSendingOptionsExt
            {
                Body = "Test SMS"
            }
        };

        // Act & Assert
        validator.TestValidate(recipient)
            .ShouldHaveValidationErrorFor(r => r.EmailSettings);
    }

    [Fact]
    public void Validate_MissingSmsSettings_ShouldHaveError()
    {
        // Arrange
        var validator = new RecipientEmailAndSmsValidator();
        var recipient = new RecipientEmailAndSmsExt
        {
            EmailAddress = "test@example.com",
            PhoneNumber = "+4799999999",
            EmailSettings = new EmailSendingOptionsExt
            {
                Subject = "Test Subject",
                Body = "Test Body",
                ContentType = EmailContentTypeExt.Plain
            },
            SmsSettings = null!
        };

        // Act & Assert
        validator.TestValidate(recipient)
            .ShouldHaveValidationErrorFor(r => r.SmsSettings);
    }
}
