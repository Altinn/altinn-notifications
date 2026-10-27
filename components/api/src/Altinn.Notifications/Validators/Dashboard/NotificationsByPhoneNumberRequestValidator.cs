using Altinn.Notifications.Core.Helpers;
using Altinn.Notifications.Models.Dashboard;

using FluentValidation;

namespace Altinn.Notifications.Validators.Dashboard;

/// <summary>
/// Validator for <see cref="NotificationsByPhoneNumberRequestExt"/>.
/// </summary>
internal sealed class NotificationsByPhoneNumberRequestValidator : AbstractValidator<NotificationsByPhoneNumberRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationsByPhoneNumberRequestValidator"/> class.
    /// </summary>
    public NotificationsByPhoneNumberRequestValidator()
    {
        Include(new DashboardNotificationRequestValidator());

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("'PhoneNumber' is required and cannot be empty")
            .Must(MobileNumberHelper.IsValidMobileNumber).WithMessage("'PhoneNumber' must be a valid mobile number.");
    }
}
