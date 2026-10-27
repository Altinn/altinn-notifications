using Altinn.Notifications.Models.Dashboard;
using Altinn.Notifications.Validators.Rules;
using FluentValidation;

namespace Altinn.Notifications.Validators.Dashboard;

/// <summary>
/// Validator for <see cref="NotificationsByOrgNumberRequestExt"/>.
/// </summary>
internal sealed class NotificationsByOrgNumberRequestValidator : AbstractValidator<NotificationsByOrgNumberRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationsByOrgNumberRequestValidator"/> class.
    /// </summary>
    public NotificationsByOrgNumberRequestValidator()
    {
        Include(new DashboardNotificationRequestValidator());

        RuleFor(x => x.OrganizationNumber)
            .NotEmpty().WithMessage("'OrganizationNumber' is required and cannot be empty")
            .MustBeValidOrganizationNumber();
    }
}
