using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Email;
using FluentValidation.TestHelper;
using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class EmailSendingOptionsValidatorTests
{
    private readonly EmailSendingOptionsValidator _validator = new();

    [Fact]
    public void EmailSendingOptionsValidator_ValidateEmailSendingOptions_ValidEmailSendingOptions()
    {
        // Arrange
        var emailSendingOptions = new EmailSendingOptionsExt
        {
            Subject = "Test subject",
            Body = "Test body",
        };

        // Act
        var actual = _validator.TestValidate(emailSendingOptions);

        // Assert
        actual.ShouldNotHaveValidationErrorFor(options => options.Subject);
        actual.ShouldNotHaveValidationErrorFor(options => options.Body);
        actual.ShouldHaveValidationErrorFor(options => options.SenderEmailAddress);
    }

    [Theory]
    [InlineData("thisisinvalid", false)]
    [InlineData("noreply@altinn.no", true)]
    [InlineData("", false)]
    public void ValidateEmailSendingOptionsValidator_ValidateEmailSendingOptions_(string email, bool shouldValidateSuccessfully)
    {
        // Arrange
        var emailSendingOptions = new EmailSendingOptionsExt
        {
            Subject = "Test subject",
            Body = "Test body",
            SenderEmailAddress = email
        };

        // Act
        var actual = _validator.TestValidate(emailSendingOptions);

        // Assert
        if (shouldValidateSuccessfully)
        {
            actual.ShouldNotHaveValidationErrorFor(options => options.SenderEmailAddress);
        }
        else
        {
            actual.ShouldHaveValidationErrorFor(options => options.SenderEmailAddress);
        }
    }
}
