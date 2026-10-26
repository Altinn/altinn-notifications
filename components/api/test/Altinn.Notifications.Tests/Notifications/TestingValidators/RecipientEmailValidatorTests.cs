using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Validators.Email;

using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class RecipientEmailValidatorTests
{
    private readonly RecipientEmailValidator _recipientEmailValidator = new();

    [Theory]
    [InlineData(SendingTimePolicyExt.Anytime)]
    [InlineData(SendingTimePolicyExt.Daytime)]
    public void Should_Accept_SendingTimePolicy_Anytime_And_Daytime(SendingTimePolicyExt sendingTimePolicy)
    {
        // Arrange
        var recipientEmail = new RecipientEmailExt
        {
            EmailAddress = "recipient@altinn.xyz",
            Settings = new EmailSendingOptionsExt
            {
                Subject = "Test subject",
                Body = "Test body",
                SendingTimePolicy = sendingTimePolicy
            }
        };

        // Act
        var actual = _recipientEmailValidator.TestValidate(recipientEmail);

        // Assert
        actual.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_SendingTimePolicy_Is_Invalid()
    {
        // Arrange
        var recipientEmail = new RecipientEmailExt
        {
            EmailAddress = "recipient@altinn.xyz",
            Settings = new EmailSendingOptionsExt
            {
                Subject = "Test subject",
                Body = "Test body",
                SendingTimePolicy = (SendingTimePolicyExt)999 // Invalid value
            }
        };

        // Act
        var actual = _recipientEmailValidator.TestValidate(recipientEmail);

        // Assert
        actual.ShouldHaveValidationErrorFor(options => options.Settings.SendingTimePolicy)
            .WithErrorMessage("Email only supports send time daytime and anytime");
    }
}
