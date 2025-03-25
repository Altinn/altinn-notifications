using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Recipient;
using FluentValidation.TestHelper;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators
{
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
                ChannelScheme = NotificationChannelExt.Sms,
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
                ChannelScheme = NotificationChannelExt.SmsPreferred
            };

            // act
            var actual = _recipientPersonValidator.TestValidate(recipientPerson);

            // assert
            actual.ShouldHaveValidationErrorFor(recipient => recipient.EmailSettings).WithErrorMessage("EmailSettings must be set when ChannelScheme is SmsPreffered or EmailPreferred");
            actual.ShouldHaveValidationErrorFor(recipient => recipient.SmsSettings).WithErrorMessage("SmsSettings must be set when ChannelScheme is SmsPreffered or EmailPreferred");
        }

        [Fact]
        public void Should_Have_Validation_Error_For_Email_When_Missing_Recipient_Using_Preferred_Scheme()
        {
            // arrange
            var recipientPerson = new RecipientPersonExt
            {
                NationalIdentityNumber = "12345678910",
                ChannelScheme = NotificationChannelExt.SmsPreferred,
                SmsSettings = new SmsSendingOptionsExt
                {
                    Sender = "Test sender",
                    Body = "Hello world"
                }
            };

            // act
            var actual = _recipientPersonValidator.TestValidate(recipientPerson);

            // assert
            actual.ShouldHaveValidationErrorFor(recipient => recipient.EmailSettings).WithErrorMessage("EmailSettings must be set when ChannelScheme is SmsPreffered or EmailPreferred");
            actual.ShouldNotHaveValidationErrorFor(recipient => recipient.SmsSettings);
        }

        [Fact]
        public void Should_NOT_Have_Validation_Error_For_Email_When_Missing_Recipient_Using_Sms_Scheme()
        {
            // arrange
            var recipientPerson = new RecipientPersonExt
            {
                NationalIdentityNumber = "12345678910",
                ChannelScheme = NotificationChannelExt.Sms,
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
    }
}
