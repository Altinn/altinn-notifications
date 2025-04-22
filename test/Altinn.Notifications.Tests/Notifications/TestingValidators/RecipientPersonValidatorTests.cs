using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators.Recipient;

using FluentValidation.TestHelper;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class RecipientPersonValidatorTests
{
    private readonly RecipientPersonValidator _recipientPersonValidator = new();

    [Theory]
    [InlineData("123456789", "National identity number must be 11 digits long.")]
    [InlineData("", "'National Identity Number' must not be empty.")]
    public void Should_Have_Validation_Error_For_NationalIdentityNumber_When_Invalid_Length(string nin, string errorMessage)
    {
        // arrange
        var recipientPerson = new RecipientPersonExt
        {
            NationalIdentityNumber = nin,
            ChannelSchema = NotificationChannelExt.Sms,
        };

        // act
        var actual = _recipientPersonValidator.TestValidate(recipientPerson);

        // assert
        actual.ShouldHaveValidationErrorFor(recipient => recipient.NationalIdentityNumber).WithErrorMessage(errorMessage);
    }

    [Fact]
    public void Should_Have_Validation_Errors_When_Missing_Recipients_Using_Preferred_Scheme()
    {
        // arrange
        var recipientPerson = new RecipientPersonExt
        {
            NationalIdentityNumber = "12345678910",
            ChannelSchema = NotificationChannelExt.SmsPreferred
        };

        // act
        var actual = _recipientPersonValidator.TestValidate(recipientPerson);

        // assert
        actual.ShouldHaveValidationErrorFor(recipient => recipient.EmailSettings).WithErrorMessage("EmailSettings must be set when ChannelSchema is SmsPreferred or EmailPreferred");
        actual.ShouldHaveValidationErrorFor(recipient => recipient.SmsSettings).WithErrorMessage("SmsSettings must be set when ChannelSchema is SmsPreferred or EmailPreferred");
    }

    [Fact]
    public void Should_Have_Validation_Error_For_Email_When_Missing_Recipient_Using_Preferred_Scheme()
    {
        // arrange
        var recipientPerson = new RecipientPersonExt
        {
            NationalIdentityNumber = "12345678910",
            ChannelSchema = NotificationChannelExt.SmsPreferred,
            SmsSettings = new SmsSendingOptionsExt
            {
                Sender = "Test sender",
                Body = "Hello world"
            }
        };

        // act
        var actual = _recipientPersonValidator.TestValidate(recipientPerson);

        // assert
        actual.ShouldHaveValidationErrorFor(recipient => recipient.EmailSettings).WithErrorMessage("EmailSettings must be set when ChannelSchema is SmsPreferred or EmailPreferred");
        actual.ShouldNotHaveValidationErrorFor(recipient => recipient.SmsSettings);
    }

    [Fact]
    public void Should_NOT_Have_Validation_Error_For_Email_When_Missing_Recipient_Using_Sms_Scheme()
    {
        // arrange
        var recipientPerson = new RecipientPersonExt
        {
            NationalIdentityNumber = "12345678910",
            ChannelSchema = NotificationChannelExt.Sms,
            SmsSettings = new SmsSendingOptionsExt
            {
                Sender = "Test sender",
                Body = "Hello world"
            }
        };

        // act
        var actual = _recipientPersonValidator.TestValidate(recipientPerson);

        // assert
        actual.ShouldNotHaveValidationErrorFor(recipient => recipient.EmailSettings);
        actual.ShouldNotHaveValidationErrorFor(recipient => recipient.SmsSettings);
    }

    [Fact]
    public void Should_Validate_ResourceId_To_True_When_Null()
    {
        // arrange
        var recipientPerson = new RecipientPersonExt
        {
            NationalIdentityNumber = "12345678910",
            ResourceId = null,
            ChannelSchema = NotificationChannelExt.Sms,
        };

        // act
        var actual = _recipientPersonValidator.TestValidate(recipientPerson);

        // assert
        actual.ShouldNotHaveValidationErrorFor(recipient => recipient.ResourceId);
    }

    [Fact]
    public void Should_Not_Validate_ResourceId_To_True_When_Invalid_Prefix()
    {
        // arrange
        var recipientPerson = new RecipientPersonExt
        {
            NationalIdentityNumber = "12345678910",
            ResourceId = "burn:altinn:resource:12345678910", // invalid prefix
            ChannelSchema = NotificationChannelExt.Sms,
        };

        // act
        var actual = _recipientPersonValidator.TestValidate(recipientPerson);

        // assert
        actual.ShouldHaveValidationErrorFor(recipient => recipient.ResourceId).WithErrorMessage("ResourceId must have a valid syntax.");
    }

    [Fact]
    public void Should_Validate_ResourceId_To_True_When_Valid_Prefix()
    {
        // arrange
        var recipientPerson = new RecipientPersonExt
        {
            NationalIdentityNumber = "12345678910",
            ResourceId = "urn:altinn:resource:12345678910",
            ChannelSchema = NotificationChannelExt.Sms,
        };

        // act
        var actual = _recipientPersonValidator.TestValidate(recipientPerson);

        // assert
        actual.ShouldNotHaveValidationErrorFor(recipient => recipient.ResourceId);
    }

    [Fact]
    public void Should_Have_Validation_Errors_When_Missing_Settings_Using_EmailAndSms_Scheme()
    {
        // arrange
        var recipientPerson = new RecipientPersonExt
        {
            NationalIdentityNumber = "12345678910",
            ChannelSchema = NotificationChannelExt.EmailAndSms
        };

        // act
        var actual = _recipientPersonValidator.TestValidate(recipientPerson);

        // assert
        actual.ShouldHaveValidationErrorFor(recipient => recipient.SmsSettings).WithErrorMessage("SmsSettings must be set when ChannelSchema is EmailAndSms");
        actual.ShouldHaveValidationErrorFor(recipient => recipient.EmailSettings).WithErrorMessage("EmailSettings must be set when ChannelSchema is EmailAndSms");
    }

    [Fact]
    public void Should_NOT_Have_Validation_Errors_When_Both_Settings_Present_Using_EmailAndSms_Scheme()
    {
        // arrange
        var recipientPerson = new RecipientPersonExt
        {
            NationalIdentityNumber = "12345678910",
            ChannelSchema = NotificationChannelExt.EmailAndSms,
            EmailSettings = new EmailSendingOptionsExt
            {
                Subject = "Test subject",
                Body = "Test email body",
                SendingTimePolicy = SendingTimePolicyExt.Anytime
            },
            SmsSettings = new SmsSendingOptionsExt
            {
                Body = "Test SMS message",
                SendingTimePolicy = SendingTimePolicyExt.Daytime
            }
        };

        // act
        var actual = _recipientPersonValidator.TestValidate(recipientPerson);

        // assert
        actual.ShouldNotHaveValidationErrorFor(recipient => recipient.SmsSettings);
        actual.ShouldNotHaveValidationErrorFor(recipient => recipient.EmailSettings);
    }
}
