using Altinn.Notifications.Models.Dashboard;
using Altinn.Notifications.Validators.Rules;
using FluentValidation;

namespace Altinn.Notifications.Validators.Dashboard;

/// <summary>
/// Validator for <see cref="NotificationsByNinRequestExt"/>.
/// </summary>
internal sealed class NotificationsByNinRequestValidator : AbstractValidator<NotificationsByNinRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationsByNinRequestValidator"/> class.
    /// </summary>
    public NotificationsByNinRequestValidator()
    {
        Include(new DashboardNotificationRequestValidator());

        RuleFor(x => x.NationalIdentityNumber)
            .NotEmpty().WithMessage("'NationalIdentityNumber' is required and cannot be empty")
            .MustBeValidNationalIdentityNumber();
    }
}
