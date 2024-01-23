﻿using System;
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
}
