﻿using Altinn.Notifications.Models.Sms;
using Altinn.Notifications.Validators.Rules;
using FluentValidation;

namespace Altinn.Notifications.Validators.Sms;

/// <summary>
/// Class containing validation logic for the <see cref="SmsNotificationOrderRequestExt"/> model
/// </summary>
internal sealed class SmsNotificationOrderRequestValidator : AbstractValidator<SmsNotificationOrderRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNotificationOrderRequestValidator"/> class.
    /// </summary>
    public SmsNotificationOrderRequestValidator()
    {
        RuleFor(order => order)
            .ValidateBaseProps();

        RuleFor(order => order)
            .ValidateSmsTemplateContent();

        RuleFor(order => order.Recipients)
            .ValidateSmsRecipients();
    }
}
