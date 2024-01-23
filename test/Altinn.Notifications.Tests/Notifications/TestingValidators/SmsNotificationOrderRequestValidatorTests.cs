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
}
