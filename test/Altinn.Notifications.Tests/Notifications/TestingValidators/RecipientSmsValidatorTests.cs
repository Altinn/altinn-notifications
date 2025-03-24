using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Sms;
using FluentValidation.TestHelper;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators
{
    public class RecipientSmsValidatorTests
    {
        private readonly RecipientSmsValidator _recipientSmsValidator = new();

        [Theory]
        [InlineData("12345678", false)]
        [InlineData("+4740000000", true)]
        [InlineData("004740000000", true)]
        [InlineData("40000000", false)]
        public void ValidateRecipient_MobileNumber(string mobileNumber, bool isValidatedSuccessfully)
        {
            // arrange
            var recipientSms = new RecipientSmsExt
            {
                PhoneNumber = mobileNumber,
                Settings = new SmsSendingOptionsExt
                {
                    Body = "Test body",
                    Sender = "Test sender",
                },
            };

            // act
            var actual = _recipientSmsValidator.TestValidate(recipientSms);

            // assert
            if (isValidatedSuccessfully)
            {
                actual.ShouldNotHaveValidationErrorFor(recipient => recipient.PhoneNumber);
            }
            else
            {
                actual.ShouldHaveValidationErrorFor(recipient => recipient.PhoneNumber);
            }
        }
    }
}
