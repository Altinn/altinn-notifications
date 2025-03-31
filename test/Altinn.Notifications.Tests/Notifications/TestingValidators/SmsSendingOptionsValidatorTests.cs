using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
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
        public void Should_Not_Accept_SendingTimePolicy_When_Not_Anytime()
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
            actual.ShouldHaveValidationErrorFor(options => options.SendingTimePolicy).WithErrorMessage("SMS only supports send time daytime");
        }
    }
}
