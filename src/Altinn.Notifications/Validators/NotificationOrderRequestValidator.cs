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
        RuleFor(order => order)
            .ValidateBaseProps();

        RuleFor(order => order.NotificationChannel)
            .NotNull()
            .WithMessage("A notification channel must be defined");

        RuleFor(order => order)
               .ValidateOptionalEmailTemplate();

        RuleFor(order => order)
            .ValidateOptionalSmsTemplate();

        RuleFor(order => order.Recipients)
            .ValidatePreferredRecipients();
    }
}
