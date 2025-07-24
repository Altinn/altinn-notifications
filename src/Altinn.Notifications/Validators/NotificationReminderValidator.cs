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
        RuleFor(options => options)
            .Must(options =>
                (options.DelayDays.HasValue && !options.RequestedSendTime.HasValue) ||
                (!options.DelayDays.HasValue && options.RequestedSendTime.HasValue))
            .WithMessage("Either DelayDays or RequestedSendTime must be defined, but not both.");

        When(options => options.DelayDays.HasValue, () =>
        {
            RuleFor(options => options.DelayDays)
                .GreaterThanOrEqualTo(1)
                .WithMessage("DelayDays must be greater than or equal to 1 day.");

            RuleFor(options => options.RequestedSendTime)
                .Null()
                .WithMessage("RequestedSendTime must be null when DelayDays is set.");
        });

        When(options => options.RequestedSendTime.HasValue, () =>
        {
            RuleFor(options => options.DelayDays)
                .Null()
                .WithMessage("DelayDays must be null when RequestedSendTime is set.");

            RuleFor(option => option.RequestedSendTime)
                .Must(sendTime => sendTime!.Value.Kind != DateTimeKind.Unspecified)
                .WithMessage("The RequestedSendTime must have specified a time zone.")

                .GreaterThanOrEqualTo(DateTime.UtcNow)
                .WithMessage("RequestedSendTime must be greater than or equal to the current UTC time.");
        });

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
