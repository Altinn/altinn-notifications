using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators.Recipient;

using FluentValidation.TestHelper;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class RecipientOrganizationValidatorTests
{
    private readonly RecipientOrganizationValidator _recipientOrganizationValidator = new();

    [Fact]
    public void Should_Have_Validation_Error_For_OrgNumber_When_Invalid_Length()
    {
        // arrange
        var recipientOrganization = new RecipientOrganizationExt
        {
            OrgNumber = "123456",
            ChannelSchema = NotificationChannelExt.Sms,
        };

        // act
        var actual = _recipientOrganizationValidator.TestValidate(recipientOrganization);

        // assert
        actual.ShouldHaveValidationErrorFor(recipient => recipient.OrgNumber).WithErrorMessage("Organization number must be 9 digits long.");
    }

    [Fact]
    public void Should_Have_Validation_Error_When_OrgNumber_Is_Empty()
    {
        // arrange
        var recipientOrganization = new RecipientOrganizationExt
        {
            OrgNumber = string.Empty,
            ChannelSchema = NotificationChannelExt.Sms,
        };

        // act
        var actual = _recipientOrganizationValidator.TestValidate(recipientOrganization);

        // assert
        actual.ShouldHaveValidationErrorFor(recipient => recipient.OrgNumber).WithErrorMessage("OrgNumber cannot be null or empty.");
    }

    [Fact]
    public void Should_Have_Validation_Errors_When_Missing_Recipients_Using_Preferred_Scheme()
    {
        // arrange
        var recipientOrganization = new RecipientOrganizationExt
        {
            OrgNumber = "123456789",
            ChannelSchema = NotificationChannelExt.SmsPreferred
        };

        // act
        var actual = _recipientOrganizationValidator.TestValidate(recipientOrganization);
        
        // assert
        actual.ShouldHaveValidationErrorFor(recipient => recipient.EmailSettings).WithErrorMessage("EmailSettings must be set when ChannelSchema is SmsPreffered or EmailPreferred");
        actual.ShouldHaveValidationErrorFor(recipient => recipient.SmsSettings).WithErrorMessage("SmsSettings must be set when ChannelSchema is SmsPreffered or EmailPreferred");
    }

    [Fact]
    public void Should_Have_Validation_Error_For_Email_When_Missing_Recipient_Using_Preferred_Scheme()
    {
        // arrange
        var recipientOrganization = new RecipientOrganizationExt
        {
            OrgNumber = "123456789",
            ChannelSchema = NotificationChannelExt.SmsPreferred,
            SmsSettings = new SmsSendingOptionsExt
            {
                Sender = "Test sender",
                Body = "Hello world"
            }
        };

        // act
        var actual = _recipientOrganizationValidator.TestValidate(recipientOrganization);

        // assert
        actual.ShouldHaveValidationErrorFor(recipient => recipient.EmailSettings).WithErrorMessage("EmailSettings must be set when ChannelSchema is SmsPreffered or EmailPreferred");
        actual.ShouldNotHaveValidationErrorFor(recipient => recipient.SmsSettings);
    }

    [Fact]
    public void Should_NOT_Have_Validation_Error_For_Email_When_Missing_Recipient_Using_Sms_Scheme()
    {
        // arrange
        var recipientOrganization = new RecipientOrganizationExt
        {
            OrgNumber = "123456789",
            ChannelSchema = NotificationChannelExt.Sms,
            SmsSettings = new SmsSendingOptionsExt
            {
                Sender = "Test sender",
                Body = "Hello world"
            }
        };

        // act
        var actual = _recipientOrganizationValidator.TestValidate(recipientOrganization);

        // assert
        actual.ShouldNotHaveValidationErrorFor(recipient => recipient.EmailSettings);
        actual.ShouldNotHaveValidationErrorFor(recipient => recipient.SmsSettings);
    }
}
