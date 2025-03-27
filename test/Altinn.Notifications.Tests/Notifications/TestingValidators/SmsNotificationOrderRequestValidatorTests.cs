using System;

using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators.Sms;
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
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("Mobile number can contain only '+' and numeric characters, and it must adhere to the E.164 standard."));
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
}
