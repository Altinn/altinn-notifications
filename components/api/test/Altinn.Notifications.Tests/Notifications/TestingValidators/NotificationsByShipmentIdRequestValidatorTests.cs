using Altinn.Notifications.Models.Dashboard;
using Altinn.Notifications.Validators.Dashboard;

using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class NotificationsByShipmentIdRequestValidatorTests
{
    private readonly NotificationsByShipmentIdRequestValidator _validator = new();

    [Fact]
    public void Should_Have_Validation_Error_For_ShipmentId_When_Empty()
    {
        // arrange
        var request = new NotificationsByShipmentIdRequestExt { ShipmentId = string.Empty };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.ShipmentId).WithErrorMessage("'ShipmentId' is required and cannot be empty");
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("16069412345")]
    public void Should_Have_Validation_Error_For_ShipmentId_When_Invalid_Format(string shipmentId)
    {
        // arrange
        var request = new NotificationsByShipmentIdRequestExt { ShipmentId = shipmentId };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.ShipmentId).WithErrorMessage("'ShipmentId' must be a valid GUID.");
    }

    [Fact]
    public void Should_Not_Have_Validation_Error_For_ShipmentId_When_Valid()
    {
        // arrange
        var request = new NotificationsByShipmentIdRequestExt { ShipmentId = Guid.NewGuid().ToString() };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldNotHaveValidationErrorFor(r => r.ShipmentId);
    }
}
