using System.Collections.Generic;
using System.Linq;

using Altinn.Notifications.Models;
using Altinn.Notifications.Validators;

using Xunit;

namespace Altinn.Notifications.Tests.TestingValidators;

public class EmailNotificationOrderRequestValidatorTests
{
    private readonly EmailNotificationOrderRequestValidator _validator;

    public EmailNotificationOrderRequestValidatorTests()
    {
        _validator = new EmailNotificationOrderRequestValidator();
    }

    [Fact]
    public void Validate_RecipientProvided_ReturnsTrue()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            FromAddress = "sender@domain.com",
            Recipients = new List<RecipientExt>() { new RecipientExt() { Id = "16069412345", EmailAddress = "recipient2@domain.com" } },
            Body = "This is an email body"
        };

        var actual = _validator.Validate(order);
        Assert.True(actual.IsValid);
    }

    [Fact]
    public void Validate_ToAddressProvided_ReturnsTrue()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            FromAddress = "sender@domain.com",
            ToAddresses = new List<string>() { "recipient1@domain.com", "recipient2@domain.com" },
            Body = "This is an email body"
        };

        var actual = _validator.Validate(order);
        Assert.True(actual.IsValid);
    }

    [Fact]
    public void Validate_BothRecipientsAndToAddressPopulated_ReturnsFalse()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            FromAddress = "sender@domain.com",
            ToAddresses = new List<string>() { "recipient1@domain.com", "recipient2@domain.com" },
            Recipients = new List<RecipientExt>() { new RecipientExt() { Id = "16069412345", EmailAddress = "person@domain.com" } },
            Body = "This is an email body"

        };

        var actual = _validator.Validate(order);
        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("Provide either recipients or to addresses, not both."));
    }

    [Fact]
    public void Validate_EmailNotDefinedForRecipient_ReturnFalse()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            FromAddress = "sender@domain.com",
            Recipients = new List<RecipientExt>() { new RecipientExt() { Id = "16069412345" } },
            Body = "This is an email body"

        };
        var actual = _validator.Validate(order);

        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("Email address must be provided for all recipients"));
    }

    [Fact]
    public void Validate_FromAddressMissing_ReturnsFalse()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            Subject = "This is an email subject",
            Recipients = new List<RecipientExt>() { new RecipientExt() { Id = "16069412345", EmailAddress = "recipient2@domain.com" } },
            Body = "This is an email body"

        };

        var actual = _validator.Validate(order);
        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("'From Address' must not be empty."));

    }

    [Fact]
    public void Validate_SubjectMissing_ReturnsFalse()
    {
        var order = new EmailNotificationOrderRequestExt()
        {
            FromAddress = "sender@domain.com",
            Recipients = new List<RecipientExt>() { new RecipientExt() { Id = "16069412345", EmailAddress = "recipient2@domain.com" } },
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
            FromAddress = "sender@domain.com",
            Recipients = new List<RecipientExt>() { new RecipientExt() { Id = "16069412345", EmailAddress = "recipient2@domain.com" } },
        };

        var actual = _validator.Validate(order);
        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("'Body' must not be empty."));

    }
}
