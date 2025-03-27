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
                .NotEmpty()
                .GreaterThanOrEqualTo(DateTime.UtcNow)
                .WithMessage("RequestedSendTime is required and must be greater than or equal to now.");
        }
    }
}
