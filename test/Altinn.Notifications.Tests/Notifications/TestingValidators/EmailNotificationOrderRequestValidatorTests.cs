using System;

using Altinn.Notifications.Models;
using Altinn.Notifications.Validators;
using Altinn.Notifications.Validators.Rules;

using FluentValidation;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class EmailNotificationOrderRequestValidatorTests
{
    private readonly EmailNotificationOrderRequestValidator _validator;

    public EmailNotificationOrderRequestValidatorTests()
    {
        ValidatorOptions.Global.LanguageManager.Enabled = false;
        _validator = new EmailNotificationOrderRequestValidator();
    }

    [Fact]
    public void Validate_RecipientProvided_ReturnsTrue()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = [new RecipientExt() { EmailAddress = "recipient2@domain.com" }],
            Body = "This is an email body"
        };

        var actual = _validator.Validate(order);
        Assert.True(actual.IsValid);
    }

    [Theory]
    [InlineData("123456789", true)]
    [InlineData("12345678", false)]
    [InlineData("abc456789", false)]
    public void Validate_RecipientOrganizationNumber_MustBe9Digits(string organizationNumber, bool expectedResult)
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = [new RecipientExt() { OrganizationNumber = organizationNumber }],
            Body = "This is an email body"
        };

        var validationResult = _validator.Validate(order);
        Assert.Equal(expectedResult, validationResult.IsValid);
    }

    [Theory]
    [InlineData("16069412345", true)]
    [InlineData("ab069412345", false)]
    [InlineData("123456784651", false)]
    public void Validate_RecipientNationalIdentityNumber_MustBe11Digits(string nationalIdentityNumber, bool expectedResult)
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = [new RecipientExt() { NationalIdentityNumber = nationalIdentityNumber }],
            Body = "This is an email body"
        };

        var validationResult = _validator.Validate(order);
        Assert.Equal(expectedResult, validationResult.IsValid);
    }

    [Fact]
    public void Validate_InvalidEmailFormatProvided_ReturnsFalse()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = [new RecipientExt() { EmailAddress = "@domain.com" }],
            Body = "This is an email body"
        };

        var actual = _validator.Validate(order);

        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("Invalid email address format."));
    }

    [Fact]
    public void Validate_NoDetailsDefinedForRecipient_ReturnFalse()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = [new RecipientExt()],
            Body = "This is an email body"
        };

        var actual = _validator.Validate(order);

        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("Either a valid email address, organization number, or national identity number must be provided for each recipient."));
    }

    [Fact]
    public void Validate_SendTimeHasLocalZone_ReturnsTrue()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = [new RecipientExt() { EmailAddress = "recipient2@domain.com" }],
            Body = "This is an email body",
            RequestedSendTime = DateTime.Now
        };

        var actual = _validator.Validate(order);

        Assert.True(actual.IsValid);
    }

    [Fact]
    public void Validate_SendTimeHasUtcZone_ReturnsTrue()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = [new RecipientExt() { EmailAddress = "recipient2@domain.com" }],
            Body = "This is an email body",
            RequestedSendTime = DateTime.UtcNow
        };

        var actual = _validator.Validate(order);

        Assert.True(actual.IsValid);
    }

    [Fact]
    public void Validate_SendTimeHasNoZone_ReturnsFalse()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = [new RecipientExt() { EmailAddress = "recipient2@domain.com" }],
            Body = "This is an email body",
            RequestedSendTime = new DateTime(2023, 06, 16, 08, 50, 00, DateTimeKind.Unspecified)
        };

        var actual = _validator.Validate(order);

        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("The requested send time value must have specified a time zone."));
    }

    [Fact]
    public void Validate_SendTimePassed_ReturnsFalse()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = [new RecipientExt() { EmailAddress = "recipient2@domain.com" }],
            Body = "This is an email body",
            RequestedSendTime = DateTime.UtcNow.AddDays(-1)
        };

        var actual = _validator.Validate(order);

        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("Send time must be in the future. Leave blank to send immediately."));
    }

    [Fact]
    public void Validate_SubjectMissing_ReturnsFalse()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Recipients = [new RecipientExt() { EmailAddress = "recipient2@domain.com" }],
            Body = "This is an email body"
        };

        var actual = _validator.Validate(order);
        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("The email template subject must not be empty."));
    }

    [Fact]
    public void Validate_BodyMissing_ReturnsFalse()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = [new RecipientExt() { EmailAddress = "recipient2@domain.com" }],
        };

        var actual = _validator.Validate(order);
        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("The email template body must not be empty."));
    }

    [Fact]
    public void Validate_ConditionEndpointIsUrn_ReturnsFalse()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = [new RecipientExt() { EmailAddress = "recipient2@domain.com" }],
            Body = "This is an email body",
            ConditionEndpoint = new Uri("urn:altinn.test")
        };

        var actual = _validator.Validate(order);

        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("The condition endpoint must be a valid URL."));
    }

    [Theory]
    [InlineData("æge_sjøåsen@domain.com", true)]
    [InlineData("stephanie@kul.no", true)]
    [InlineData("bakken_kundeservice@sykkelverksted.com", true)]
    [InlineData("john.doe@sub.domain.example", true)]
    [InlineData("gratis-netflix+1@gmail.com", true)]
    [InlineData(".user@example.com", true)]
    [InlineData("", false)]
    [InlineData("userexample.com", false)]
    [InlineData("user@", false)]
    [InlineData("@example.com", false)]
    [InlineData("user@example..com", false)]
    [InlineData("user@exa!mple.com", false)]
    [InlineData("user.@example.com", false)]
    public void IsValidEmail(string email, bool expectedResult)
    {
        bool actual = RecipientRules.IsValidEmail(email);
        Assert.Equal(expectedResult, actual);
    }
}
