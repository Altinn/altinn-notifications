using Altinn.Notifications.Models.Dashboard;

using FluentValidation;

namespace Altinn.Notifications.Validators.Dashboard;

/// <summary>
/// Validator for <see cref="NotificationsByShipmentIdRequestExt"/>.
/// </summary>
internal sealed class NotificationsByShipmentIdRequestValidator : AbstractValidator<NotificationsByShipmentIdRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationsByShipmentIdRequestValidator"/> class.
    /// </summary>
    public NotificationsByShipmentIdRequestValidator()
    {
        Include(new DashboardNotificationRequestValidator());

        RuleFor(x => x.ShipmentId)
            .NotEmpty().WithMessage("'ShipmentId' is required and cannot be empty")
            .Must(id => Guid.TryParse(id, out _)).WithMessage("'ShipmentId' must be a valid GUID.");
    }
}
