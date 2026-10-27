using Altinn.Notifications.Models.Dashboard;
using Altinn.Notifications.Validators.Dashboard;

using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class NotificationsByPhoneNumberRequestValidatorTests
{
    private readonly NotificationsByPhoneNumberRequestValidator _validator = new();

    [Fact]
    public void Should_Have_Validation_Error_For_PhoneNumber_When_Empty()
    {
        // arrange
        var request = new NotificationsByPhoneNumberRequestExt { PhoneNumber = string.Empty };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.PhoneNumber).WithErrorMessage("'PhoneNumber' is required and cannot be empty");
    }

    [Theory]
    [InlineData("40000001")]
    [InlineData("111100000")]
    [InlineData("dasdsadSASA")]
    public void Should_Have_Validation_Error_For_PhoneNumber_When_Invalid_Format(string phoneNumber)
    {
        // arrange
        var request = new NotificationsByPhoneNumberRequestExt { PhoneNumber = phoneNumber };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.PhoneNumber).WithErrorMessage("'PhoneNumber' must be a valid mobile number.");
    }

    [Fact]
    public void Should_Not_Have_Validation_Error_For_PhoneNumber_When_Valid()
    {
        // arrange
        var request = new NotificationsByPhoneNumberRequestExt { PhoneNumber = "+4799999999" };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldNotHaveValidationErrorFor(r => r.PhoneNumber);
    }
}
