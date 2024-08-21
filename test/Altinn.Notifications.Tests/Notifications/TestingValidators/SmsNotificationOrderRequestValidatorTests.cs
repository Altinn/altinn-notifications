using System;

using Altinn.Notifications.Models;
using Altinn.Notifications.Validators;

using FluentValidation;

using Xunit;

namespace Altinn.Notifications.Tests.Notifications.TestingValidators;

public class SmsNotificationOrderRequestValidatorTests
{
    private readonly SmsNotificationOrderRequestValidator _validator;

    public SmsNotificationOrderRequestValidatorTests()
    {
        ValidatorOptions.Global.LanguageManager.Enabled = false;
        _validator = new SmsNotificationOrderRequestValidator();
    }

    [Fact]
    public void Validate_RecipientProvidedForSms_ReturnsTrue()
    {
        var order = new SmsNotificationOrderRequestExt()
        {
            SenderNumber = "+4740000000",
            Recipients = [new RecipientExt() { MobileNumber = "+4740000001" }],
            Body = "This is an SMS body"
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
        var order = new SmsNotificationOrderRequestExt()
        {
            SenderNumber = "+4740000000",
            Recipients = [new RecipientExt() { OrganizationNumber = organizationNumber }],
            Body = "This is an SMS body"
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
        var order = new SmsNotificationOrderRequestExt()
        {
            SenderNumber = "+4740000000",
            Recipients = [new RecipientExt() { NationalIdentityNumber = nationalIdentityNumber }],
            Body = "This is an SMS body"
        };

        var validationResult = _validator.Validate(order);
        Assert.Equal(expectedResult, validationResult.IsValid);
    }

    [Fact]
    public void Validate_InvalidMobileNumberFormatProvided_ReturnsFalse()
    {
        var order = new SmsNotificationOrderRequestExt()
        {
            SenderNumber = "+4740000000",
            Recipients = [new RecipientExt() { MobileNumber = "1111000000" }],
            Body = "This is an SMS body"
        };

        var actual = _validator.Validate(order);

        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("Invalid mobile number format."));
    }

    [Fact]
    public void Validate_SmsNotDefinedForRecipient_ReturnFalse()
    {
        var order = new SmsNotificationOrderRequestExt()
        {
            SenderNumber = "+4740000000",
            Recipients = [new RecipientExt()],
            Body = "This is an SMS body"
        };

        var actual = _validator.Validate(order);

        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("Either a valid mobile number starting with country code, organization number, or national identity number must be provided for each recipient."));
    }

    [Fact]
    public void Validate_ForSmsSendTimeHasLocalZone_ReturnsTrue()
    {
        var order = new SmsNotificationOrderRequestExt()
        {
            SenderNumber = "+4740000001",
            Recipients = [new RecipientExt() { MobileNumber = "+4740000000" }],
            Body = "This is an SMS body",
            RequestedSendTime = DateTime.Now
        };

        var actual = _validator.Validate(order);

        Assert.True(actual.IsValid);
    }

    [Fact]
    public void Validate_ForSmsSendTimeHasUtcZone_ReturnsTrue()
    {
        var order = new SmsNotificationOrderRequestExt()
        {
            SenderNumber = "+4740000001",
            Recipients = [new RecipientExt() { MobileNumber = "+4740000000" }],
            Body = "This is an SMS body",
            RequestedSendTime = DateTime.UtcNow
        };

        var actual = _validator.Validate(order);

        Assert.True(actual.IsValid);
    }

    [Fact]
    public void Validate_ForSmsSendTimeHasNoZone_ReturnsFalse()
    {
        var order = new SmsNotificationOrderRequestExt()
        {
            SenderNumber = "+4740000001",
            Recipients = [new RecipientExt() { MobileNumber = "+4740000000" }],
            Body = "This is an SMS body",
            RequestedSendTime = new DateTime(2023, 06, 16, 08, 50, 00, DateTimeKind.Unspecified)
        };

        var actual = _validator.Validate(order);

        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("The requested send time value must have specified a time zone."));
    }

    [Fact]
    public void Validate_SendTimePassed_ReturnsFalse()
    {
        var order = new SmsNotificationOrderRequestExt()
        {
            SenderNumber = "+4740000001",
            Recipients = [new RecipientExt() { MobileNumber = "+4740085041" }],
            Body = "This is an SMS body",
            RequestedSendTime = DateTime.UtcNow.AddDays(-1)
        };

        var actual = _validator.Validate(order);

        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("Send time must be in the future. Leave blank to send immediately."));
    }

    [Fact]
    public void Validate_BodyMissing_ReturnsFalse()
    {
        var order = new SmsNotificationOrderRequestExt()
        {
            SenderNumber = "+4740000001",
            Recipients = [new RecipientExt() { MobileNumber = "+4740000000" }],
        };

        var actual = _validator.Validate(order);
        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("The SMS template body must not be empty."));
    }

    [Fact]
    public void Validate_ConditionEndpointIsUrn_ReturnsFalse()
    {
        var order = new SmsNotificationOrderRequestExt()
        {
            SenderNumber = "+4740000001",
            Recipients = [new RecipientExt() { MobileNumber = "+4740000000" }],
            Body = "This is an SMS body",
            ConditionEndpoint = new Uri("urn:altinn.test")
        };

        var actual = _validator.Validate(order);

        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("The condition endpoint must be a valid URL."));
    }

    [Theory]
    [InlineData("altinn.no", false)]
    [InlineData("No URLs here!", true)]
    [InlineData("http://localhost", false)]
    [InlineData("http://example.com", false)]
    [InlineData("http://192.168.1.1", false)]
    [InlineData("https://example.com", false)]
    [InlineData("http://127.0.0.1:8080", false)]
    [InlineData("user:pass@example.com", false)]
    [InlineData("ftp://example.com/resource", false)]
    [InlineData("http://example.com/path/with%20spaces", false)]
    [InlineData("Visit http://example.com for more info.", false)]
    [InlineData("http://example.com/path/with%2Fencoded%2Fslashes", false)]
    [InlineData("Text with newlines\nhttp://example.com\nMore text", false)]
    [InlineData("Check out https://www.example.com/path?query=string.", false)]
    [InlineData("Complete your payment here: www.example.com/payment.", false)]
    [InlineData("Check your account details here: https://my-secure.example.com.", false)]
    [InlineData("Please verify your account at http://www.example[dot]com/login.", false)]
    [InlineData("Your invoice is overdue. Pay now at http://bit.ly/securepayment.", false)]
    [InlineData("Your invoice is overdue. Pay now at http%3A%2F%2Fbit.ly%2Fsecurepayment.", false)]
    [InlineData("Click here to claim your prize: http://example.com/win?user=you&id=12345.", false)]
    [InlineData("Encoded: %68%74%74%70%3A%2F%2F%77%77%77%2E%65%78%61%6D%70%6C%65%2E%63%6F%6D", false)]
    [InlineData("To update your billing information, visit http://not-really-example.com/update.", false)]
    [InlineData("Your account requires verification. Please go to https://example.com:8080/verify.", false)]
    [InlineData("Contact us immediately at support@example.com or visit http://example.com/support.", false)]
    [InlineData("You have received a new secure message. Please review it at http://mail.example.com.", false)]
    [InlineData("Please verify your accounts: http://example.com/login and https://another-example.com.", false)]
    [InlineData("Important notice: <a href='http://example.com/reset-password'>Reset your password here</a>.", false)]
    [InlineData("For immediate action, visit http%3A%2F%2F192.168.1.1%2Fsecurity%2Fupdate to secure your account.", false)]
    [InlineData("Alert: Unusual activity detected on your account. Log in at https://login.example-bank.com immediately.", false)]
    [InlineData("https://example.com/very-long-url-with-many-segments/that-are-really-long-and-may-cause-performance-issues", false)]
    [InlineData("Your package is waiting for delivery. Confirm your address here: https://secure.example.com/track/package12345.", false)]
    [InlineData("Dear user, your account has been compromised. Please verify your details at http://example.com/login to avoid suspension.", false)]
    [InlineData("Dear user, your account has been compromised. Please verify your details at http%3A%2F%2Fexample.com%2Flogin to avoid suspension.", false)]
    public void Validate_BodyMustNotContainUrl(string messageBody, bool expectedResult)
    {
        var order = new SmsNotificationOrderRequestExt()
        {
            SenderNumber = "+4740000001",
            Recipients = [new RecipientExt() { MobileNumber = "+4740000000" }],
            Body = messageBody
        };

        var validationResult = _validator.Validate(order);
        Assert.Equal(expectedResult, validationResult.IsValid);
    }
}
