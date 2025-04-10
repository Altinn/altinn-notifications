using Altinn.Notifications.Models;

using FluentValidation;

namespace Altinn.Notifications.Validators
{
    /// <summary>
    /// Validator for <see cref="NotificationOrderBaseExt"/>.
    /// </summary>
    internal sealed class NotificationOrderBaseValidator : AbstractValidator<NotificationOrderBaseExt>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationOrderBaseValidator"/> class.
        /// </summary>
        public NotificationOrderBaseValidator()
        {
            RuleFor(option => option.RequestedSendTime)
                .Must(sendTime => sendTime.Kind != DateTimeKind.Unspecified)
                .WithMessage("The requested send time value must have specified a time zone.")
                .GreaterThanOrEqualTo(DateTime.UtcNow)
                .WithMessage("RequestedSendTime is required and must be greater than or equal to now.");

            RuleFor(option => option.ConditionEndpoint)
                .Must(conditionEndpoint => conditionEndpoint == null || Uri.IsWellFormedUriString(conditionEndpoint.ToString(), UriKind.Absolute))
                .WithMessage("ConditionEndpoint must be a valid absolute URI or null.");
        }
    }
}
