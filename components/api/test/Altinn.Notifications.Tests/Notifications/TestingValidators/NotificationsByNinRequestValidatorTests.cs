using Altinn.Notifications.Models.Dashboard;
using Altinn.Notifications.Validators.Dashboard;

using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class NotificationsByNinRequestValidatorTests
{
    private readonly NotificationsByNinRequestValidator _validator = new();

    [Fact]
    public void Should_Have_Validation_Error_For_NationalIdentityNumber_When_Empty()
    {
        // arrange
        var request = new NotificationsByNinRequestExt { NationalIdentityNumber = string.Empty };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.NationalIdentityNumber).WithErrorMessage("'NationalIdentityNumber' is required and cannot be empty");
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    [InlineData("1234567890a")]
    public void Should_Have_Validation_Error_For_NationalIdentityNumber_When_Invalid(string nin)
    {
        // arrange
        var request = new NotificationsByNinRequestExt { NationalIdentityNumber = nin };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.NationalIdentityNumber).WithErrorMessage("National identity number must be 11 digits long.");
    }

    [Fact]
    public void Should_Not_Have_Validation_Error_For_NationalIdentityNumber_When_Valid()
    {
        // arrange
        var request = new NotificationsByNinRequestExt { NationalIdentityNumber = "16069412345" };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldNotHaveValidationErrorFor(r => r.NationalIdentityNumber);
    }
}
