using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Rules;
using FluentValidation;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Represents validation logic for the notification reminder request model.
/// </summary>
internal sealed class NotificationReminderValidator : AbstractValidator<NotificationReminderExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationReminderValidator"/> class.
    /// </summary>
    public NotificationReminderValidator()
    {
        RuleFor(options => options.DelayDays)
            .GreaterThan(0)
            .WithMessage("DelayDays must be greater than 0.");

        When(options => options.ConditionEndpoint != null, () =>
        {
            RuleFor(options => options.ConditionEndpoint!)
                .ValidateConditionEndpoint();
        });

        RuleFor(options => options.Recipient)
            .NotNull()
            .SetValidator(new NotificationRecipientValidator());
    }
}
