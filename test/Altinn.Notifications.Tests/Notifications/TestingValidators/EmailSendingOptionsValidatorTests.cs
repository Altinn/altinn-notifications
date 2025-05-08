using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Validators.Email;

using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class EmailSendingOptionsValidatorTests
{
    private readonly EmailSendingOptionsValidator _validator = new();

    [Fact]
    public void Should_Validate_To_True_When_SenderEmailAddress_Is_Null()
    {
        // Arrange
        var emailSendingOptions = new EmailSendingOptionsExt
        {
            Subject = "Test subject",
            Body = "Test body",
            SenderEmailAddress = null
        };

        // Act
        var actual = _validator.TestValidate(emailSendingOptions);

        // Assert
        actual.ShouldNotHaveValidationErrorFor(options => options.SenderEmailAddress);
    }

    [Theory]
    [InlineData("thisisinvalid", false)]
    [InlineData("noreply@altinn.no", true)]
    [InlineData("", false)]
    [InlineData(null, true)]
    public void Should_ValidateSenderEmailAddress_When_PropertyIsSet(string? email, bool shouldValidateSuccessfully)
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

    [Fact]
    public void Should_Not_Accept_SendingTimePolicy_When_Not_Anytime()
    {
        // Arrange
        var emailSendingOptions = new EmailSendingOptionsExt
        {
            Subject = "Test subject",
            Body = "Test body",
            SendingTimePolicy = SendingTimePolicyExt.Daytime
        };

        // Act
        var actual = _validator.TestValidate(emailSendingOptions);
        
        // Assert
        actual.ShouldHaveValidationErrorFor(options => options.SendingTimePolicy).WithErrorMessage("Email only supports send time anytime");
    }

    [Fact]
    public void Should_Reject_Empty_Subject()
    {
        // Arrange
        var emailSendingOptions = new EmailSendingOptionsExt
        {
            Subject = string.Empty,
            Body = "Test body"
        };

        // Act
        var actual = _validator.TestValidate(emailSendingOptions);

        // Assert
        actual.ShouldHaveValidationErrorFor(options => options.Subject).WithErrorMessage("The email subject must not be empty.");
    }

    [Fact]
    public void Should_Reject_Empty_Body()
    {
        // Arrange
        var emailSendingOptions = new EmailSendingOptionsExt
        {
            Subject = "Test subject",
            Body = string.Empty
        };

        // Act
        var actual = _validator.TestValidate(emailSendingOptions);

        // Assert
        actual.ShouldHaveValidationErrorFor(options => options.Body).WithErrorMessage("The email body must not be empty.");
    }
}
