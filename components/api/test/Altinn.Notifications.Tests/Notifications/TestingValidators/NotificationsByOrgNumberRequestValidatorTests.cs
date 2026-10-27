using Altinn.Notifications.Models.Dashboard;
using Altinn.Notifications.Validators.Dashboard;

using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class NotificationsByOrgNumberRequestValidatorTests
{
    private readonly NotificationsByOrgNumberRequestValidator _validator = new();

    [Fact]
    public void Should_Have_Validation_Error_For_OrganizationNumber_When_Empty()
    {
        // arrange
        var request = new NotificationsByOrgNumberRequestExt { OrganizationNumber = string.Empty };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.OrganizationNumber).WithErrorMessage("'OrganizationNumber' is required and cannot be empty");
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("12345678a")]
    public void Should_Have_Validation_Error_For_OrganizationNumber_When_Invalid(string orgNumber)
    {
        // arrange
        var request = new NotificationsByOrgNumberRequestExt { OrganizationNumber = orgNumber };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.OrganizationNumber).WithErrorMessage("Organization number must be 9 digits long.");
    }

    [Fact]
    public void Should_Not_Have_Validation_Error_For_OrganizationNumber_When_Valid()
    {
        // arrange
        var request = new NotificationsByOrgNumberRequestExt { OrganizationNumber = "123456789" };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldNotHaveValidationErrorFor(r => r.OrganizationNumber);
    }
}
