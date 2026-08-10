using Altinn.Notifications.Models.Dashboard;
using Altinn.Notifications.Validators.Dashboard;

using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class NotificationsByEmailRequestValidatorTests
{
    private readonly NotificationsByEmailRequestValidator _validator = new();

    [Fact]
    public void Should_Have_Validation_Error_For_Email_When_Empty()
    {
        // arrange
        var request = new NotificationsByEmailRequestExt { Email = string.Empty };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.Email).WithErrorMessage("'Email' is required and cannot be empty");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-at-sign.com")]
    [InlineData("user@")]
    public void Should_Have_Validation_Error_For_Email_When_Invalid_Format(string email)
    {
        // arrange
        var request = new NotificationsByEmailRequestExt { Email = email };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.Email).WithErrorMessage("'Email' must be a valid email address.");
    }

    [Fact]
    public void Should_Not_Have_Validation_Error_For_Email_When_Valid()
    {
        // arrange
        var request = new NotificationsByEmailRequestExt { Email = "recipient@example.com" };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldNotHaveValidationErrorFor(r => r.Email);
    }
}
