using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class NotificationRecipientValidatorTests
{
    private readonly NotificationRecipientValidator _validator = new();

    [Fact]
    public void Should_Have_No_Validation_Errors_When_Only_One_Recipient()
    {
        // Arrange
        var recipient = new NotificationRecipientExt
        {
            RecipientEmail = new RecipientEmailExt
            {
                EmailAddress = "noreply@digdir.no",
                Settings = new EmailSendingOptionsExt
                {
                    Body = "Test body",
                    Subject = "Test subject",
                }
            }
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldNotHaveValidationErrorFor(recipient => recipient.RecipientEmail);
    }

    [Fact]
    public void Should_Use_Child_Validator_When_Invalid_Email()
    {
        // Arrange
        var recipient = new NotificationRecipientExt
        {
            RecipientEmail = new RecipientEmailExt
            {
                EmailAddress = "noreply",
                Settings = new EmailSendingOptionsExt
                {
                    Body = "Test body",
                    Subject = "Test subject",
                }
            }
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldHaveValidationErrorFor(recipient => recipient!.RecipientEmail!.EmailAddress).WithErrorMessage("Invalid email address format.");
    }

    [Fact]
    public void Should_Have_Validation_Error_When_No_Recipient()
    {
        // Arrange
        var recipient = new NotificationRecipientExt();

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldHaveValidationErrorFor(recipient => recipient);
    }

    [Fact]
    public void Should_Have_Validation_Error_When_Multiple_Recipients()
    {
        // Arrange
        var recipient = new NotificationRecipientExt
        {
            RecipientEmail = new RecipientEmailExt
            {
                EmailAddress = "noreply@digdir.no",
                Settings = new EmailSendingOptionsExt
                {
                    Body = "Test body",
                    Subject = "Test subject",
                }
            },
            RecipientSms = new RecipientSmsExt
            {
                PhoneNumber = "+4740000000",
                Settings = new SmsSendingOptionsExt
                {
                    Sender = "Test message",
                    Body = "hello world"
                }
            }
        };

        // Act
        var actual = _validator.TestValidate(recipient);

        // Assert
        actual.ShouldHaveValidationErrorFor(recipient => recipient).WithErrorMessage("Must have exactly one recipient.");
    }
}
