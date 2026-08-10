using System;

using Altinn.Notifications.Models.Dashboard;
using Altinn.Notifications.Validators.Dashboard;

using FluentValidation.TestHelper;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class DashboardNotificationRequestValidatorTests
{
    private readonly DashboardNotificationRequestValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Validation_Error_When_From_And_To_Are_Null()
    {
        // arrange
        var request = new DashboardNotificationRequestExt();

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldNotHaveValidationErrorFor(r => r.From);
        actual.ShouldNotHaveValidationErrorFor(r => r.To);
    }

    [Fact]
    public void Should_Have_Validation_Error_For_From_When_Not_Utc()
    {
        // arrange
        var request = new DashboardNotificationRequestExt
        {
            From = new DateTime(2026, 05, 01, 0, 0, 0, DateTimeKind.Local)
        };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.From).WithErrorMessage("The 'from' value must be UTC.");
    }

    [Fact]
    public void Should_Have_Validation_Error_For_To_When_Not_Utc()
    {
        // arrange
        var request = new DashboardNotificationRequestExt
        {
            To = new DateTime(2026, 05, 01, 0, 0, 0, DateTimeKind.Unspecified)
        };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.To).WithErrorMessage("The 'to' value must be UTC.");
    }

    [Fact]
    public void Should_Have_Validation_Error_For_From_When_Equal_To_To()
    {
        // arrange
        var instant = new DateTime(2026, 05, 01, 0, 0, 0, DateTimeKind.Utc);
        var request = new DashboardNotificationRequestExt { From = instant, To = instant };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.From).WithErrorMessage("'from' must be earlier than 'to'.");
    }

    [Fact]
    public void Should_Have_Validation_Error_For_From_When_After_To()
    {
        // arrange
        var request = new DashboardNotificationRequestExt
        {
            From = new DateTime(2026, 05, 10, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 05, 01, 0, 0, 0, DateTimeKind.Utc)
        };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.From).WithErrorMessage("'from' must be earlier than 'to'.");
    }

    [Fact]
    public void Should_Not_Have_Validation_Error_When_From_Is_Before_To()
    {
        // arrange
        var request = new DashboardNotificationRequestExt
        {
            From = new DateTime(2026, 05, 01, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 05, 10, 0, 0, 0, DateTimeKind.Utc)
        };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldNotHaveValidationErrorFor(r => r.From);
        actual.ShouldNotHaveValidationErrorFor(r => r.To);
    }

    [Fact]
    public void Should_Have_Validation_Error_For_From_When_In_Future()
    {
        // arrange
        var request = new DashboardNotificationRequestExt { From = DateTime.UtcNow.AddDays(1) };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.From).WithErrorMessage("'from' must not be in the future.");
    }

    [Fact]
    public void Should_Have_Validation_Error_For_From_When_More_Than_10_Years_Ago()
    {
        // arrange
        var request = new DashboardNotificationRequestExt { From = DateTime.UtcNow.AddYears(-10).AddDays(-1) };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.From).WithErrorMessage("'from' must not be earlier than 10 years ago.");
    }

    [Fact]
    public void Should_Not_Have_Validation_Error_For_From_When_Within_10_Years()
    {
        // arrange
        var request = new DashboardNotificationRequestExt { From = DateTime.UtcNow.AddYears(-9) };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldNotHaveValidationErrorFor(r => r.From);
    }

    [Fact]
    public void Should_Have_Validation_Error_For_To_When_In_Future()
    {
        // arrange
        var request = new DashboardNotificationRequestExt { To = DateTime.UtcNow.AddDays(1) };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.To).WithErrorMessage("'to' must not be in the future.");
    }

    [Fact]
    public void Should_Have_Validation_Error_For_To_When_Only_To_Provided_And_Too_Far_In_Past()
    {
        // arrange — To is more than 7 days in the past with no From, which the validator rejects
        var request = new DashboardNotificationRequestExt { To = DateTime.UtcNow.AddDays(-8) };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldHaveValidationErrorFor(r => r.To).WithErrorMessage("'to' must be later than the default 'from' (7 days ago).");
    }

    [Fact]
    public void Should_Not_Have_Validation_Error_For_To_When_Only_To_Provided_Within_7_Days()
    {
        // arrange
        var request = new DashboardNotificationRequestExt { To = DateTime.UtcNow.AddDays(-1) };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldNotHaveValidationErrorFor(r => r.To);
    }

    [Fact]
    public void Should_Not_Have_Validation_Error_For_To_When_Too_Far_In_Past_But_From_Also_Provided()
    {
        // arrange — the "default 'from' is 7 days ago" rule only applies when 'From' is absent
        var request = new DashboardNotificationRequestExt
        {
            From = DateTime.UtcNow.AddDays(-30),
            To = DateTime.UtcNow.AddDays(-8)
        };

        // act
        var actual = _validator.TestValidate(request);

        // assert
        actual.ShouldNotHaveValidationErrorFor(r => r.To);
    }
}
