using System;
using System.Collections.Generic;

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
            Recipients = new List<RecipientExt>() { new RecipientExt() { MobileNumber = "+4740000001" } },
            Body = "This is an SMS body"
        };

        var actual = _validator.Validate(order);
        Assert.True(actual.IsValid);
    }

    [Fact]
    public void Validate_InvalidMobileNumberFormatProvided_ReturnsFalse()
    {
        var order = new SmsNotificationOrderRequestExt()
        {
            SenderNumber = "+4740000000",
            Recipients = new List<RecipientExt>() { new RecipientExt() { MobileNumber = "1111000000" } },
            Body = "This is an SMS body"
        };

        var actual = _validator.Validate(order);

        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("A valid mobile number starting with country code must be provided for all recipients."));
    }

    [Fact]
    public void Validate_SmsNotDefinedForRecipient_ReturnFalse()
    {
        var order = new SmsNotificationOrderRequestExt()
        {
            SenderNumber = "+4740000000",
            Recipients = new List<RecipientExt>() { new RecipientExt() },
            Body = "This is an SMS body"
        };

        var actual = _validator.Validate(order);

        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("A valid mobile number starting with country code must be provided for all recipients."));
    }

    [Fact]
    public void Validate_ForSmsSendTimeHasLocalZone_ReturnsTrue()
    {
        var order = new SmsNotificationOrderRequestExt()
        {
            SenderNumber = "+4740000001",
            Recipients = new List<RecipientExt>() { new RecipientExt() { MobileNumber = "+4740000000" } },
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
            Recipients = new List<RecipientExt>() { new RecipientExt() { MobileNumber = "+4740000000" } },
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
            Recipients = new List<RecipientExt>() { new RecipientExt() { MobileNumber = "+4740000000" } },
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
            Recipients = new List<RecipientExt>() { new RecipientExt() { MobileNumber = "+4740085041" } },
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
            Recipients = new List<RecipientExt>() { new RecipientExt() { MobileNumber = "+4740000000" } },
        };

        var actual = _validator.Validate(order);
        Assert.False(actual.IsValid);
        Assert.Contains(actual.Errors, a => a.ErrorMessage.Equals("'Body' must not be empty."));
    }

    [Theory]
    [InlineData("+4740000001", true)]
    [InlineData("004740000000", true)]
    [InlineData("40000001", false)]
    [InlineData("90000000", false)]
    [InlineData("+4790000000", true)]
    [InlineData("+4750000004", false)]
    [InlineData("+47900000001", false)]
    [InlineData("+14790000000", false)]
    [InlineData("004790000002", true)]
    [InlineData("", false)]
    [InlineData("111100000", false)]
    [InlineData("dasdsadSASA", false)]
    public void IsValidMobileNumber(string mobileNumber, bool expectedResult)
    {
        bool actual = SmsNotificationOrderRequestValidator.IsValidMobileNumber(mobileNumber);
        Assert.Equal(expectedResult, actual);
    } 
}
