using System;
using System.Collections.Generic;

using Altinn.Notifications.Models;
using Altinn.Notifications.Validators;

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
            Recipients = new List<RecipientExt>() { new RecipientExt() { EmailAddress = "recipient2@domain.com" } },
            Body = "This is an email body"
        };

        var actual = _validator.Validate(order);
        Assert.True(actual.IsValid);
    }

    [Fact]
    public void Validate_InvalidEmailFormatProvided_ReturnsFalse()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = new List<RecipientExt>() { new RecipientExt() { EmailAddress = "@domain.com" } },
            Body = "This is an email body"
        };

        var actual = _validator.Validate(order);

        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("A valid email address must be provided for all recipients."));
    }

    [Fact]
    public void Validate_EmailNotDefinedForRecipient_ReturnFalse()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = new List<RecipientExt>() { new RecipientExt() },
            Body = "This is an email body"
        };

        var actual = _validator.Validate(order);

        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("A valid email address must be provided for all recipients."));
    }

    [Fact]
    public void Validate_SendTimeHasLocalZone_ReturnsTrue()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = new List<RecipientExt>() { new RecipientExt() { EmailAddress = "recipient2@domain.com" } },
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
            Recipients = new List<RecipientExt>() { new RecipientExt() { EmailAddress = "recipient2@domain.com" } },
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
            Recipients = new List<RecipientExt>() { new RecipientExt() { EmailAddress = "recipient2@domain.com" } },
            Body = "This is an email body",
            RequestedSendTime = new DateTime(2023, 06, 16, 08, 50, 00, DateTimeKind.Unspecified)
        };

        var actual = _validator.Validate(order);

        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("No time zone specified."));
    }

    [Fact]
    public void Validate_SendTimePassed_ReturnsFalse()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = new List<RecipientExt>() { new RecipientExt() { EmailAddress = "recipient2@domain.com" } },
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
            Recipients = new List<RecipientExt>() { new RecipientExt() { EmailAddress = "recipient2@domain.com" } },
            Body = "This is an email body"
        };

        var actual = _validator.Validate(order);
        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("'Subject' must not be empty."));
    }

    [Fact]
    public void Validate_BodyMissing_ReturnsFalse()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = new List<RecipientExt>() { new RecipientExt() { EmailAddress = "recipient2@domain.com" } },
        };

        var actual = _validator.Validate(order);
        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("'Body' must not be empty."));
    }

    [Theory]
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
        bool actual = EmailNotificationOrderRequestValidator.IsValidEmail(email);
        Assert.Equal(expectedResult, actual);
    }
}
