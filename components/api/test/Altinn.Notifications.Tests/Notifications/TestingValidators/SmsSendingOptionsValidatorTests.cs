using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators.Sms;

using FluentValidation.TestHelper;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators
{
    public class SmsSendingOptionsValidatorTests
    {
        private readonly SmsSendingOptionsValidator _validator = new();

        [Fact]
        public void Should_Accept_SendingTimePolicy_When_Anytime()
        {
            // Arrange
            var smsSendingOptions = new SmsSendingOptionsExt
            {
                Sender = "Test sender",
                Body = "Test body",
                SendingTimePolicy = SendingTimePolicyExt.Anytime
            };

            // Act
            var actual = _validator.TestValidate(smsSendingOptions);

            // Assert
            actual.ShouldNotHaveValidationErrorFor(options => options.SendingTimePolicy);
        }

        [Fact]
        public void Should_Validate_Successfully_Without_Sender_Field()
        {
            // Arrange
            var smsSendingOptions = new SmsSendingOptionsExt
            {
                Body = "Test body",
                SendingTimePolicy = SendingTimePolicyExt.Daytime
            };

            // Act
            var actual = _validator.TestValidate(smsSendingOptions);

            // Assert
            actual.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Should_Fail_When_Body_Is_Not_Set()
        {
            // Arrange
            var smsSendingOptions = new SmsSendingOptionsExt
            {
                Sender = "Test sender",
                Body = string.Empty,
                SendingTimePolicy = SendingTimePolicyExt.Daytime
            };

            // Act
            var actual = _validator.TestValidate(smsSendingOptions);

            // Assert
            actual.ShouldHaveValidationErrorFor(options => options.Body)
                .WithErrorMessage("SMS body cannot be null or empty.");
        }
    }
}
