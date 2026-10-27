using Altinn.Notifications.Models.Dashboard;
using FluentValidation;

namespace Altinn.Notifications.Validators.Dashboard;

/// <summary>
/// Validator for <see cref="NotificationsByEmailRequestExt"/>.
/// </summary>
internal sealed class NotificationsByEmailRequestValidator : AbstractValidator<NotificationsByEmailRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationsByEmailRequestValidator"/> class.
    /// </summary>
    public NotificationsByEmailRequestValidator()
    {
        Include(new DashboardNotificationRequestValidator());

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("'Email' is required and cannot be empty")
            .EmailAddress().WithMessage("'Email' must be a valid email address.");
    }
}
