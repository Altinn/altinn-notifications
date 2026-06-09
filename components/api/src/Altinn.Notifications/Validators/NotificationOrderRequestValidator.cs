using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Rules;

using FluentValidation;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Class containing validation logic for the <see cref="NotificationOrderRequestExt"/> model
/// </summary>
public class NotificationOrderRequestValidator : AbstractValidator<NotificationOrderRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationOrderRequestValidator"/> class.
    /// </summary>
    public NotificationOrderRequestValidator()
    {
        RuleFor(order => order.NotificationChannel)
            .NotNull()
            .WithMessage("A notification channel must be defined.");

        RuleFor(order => order)
            .ValidateBaseProps();

        When(order => order.NotificationChannel == NotificationChannelExt.Email, () =>
        {
            RuleFor(order => order.Recipients)
                .ValidateEmailRecipients();
        });

        When(order => order.NotificationChannel == NotificationChannelExt.Sms, () =>
        {
            RuleFor(order => order.Recipients)
                .ValidateSmsRecipients();
        });

        When(order => order.NotificationChannel == NotificationChannelExt.EmailPreferred || order.NotificationChannel == NotificationChannelExt.SmsPreferred, () =>
        {
            RuleFor(order => order.Recipients)
           .ValidatePreferredRecipients();
        });

        RuleFor(order => order)
            .ValidateOptionalEmailTemplate();

        RuleFor(order => order)
            .ValidateOptionalSmsTemplate();
    }
}
