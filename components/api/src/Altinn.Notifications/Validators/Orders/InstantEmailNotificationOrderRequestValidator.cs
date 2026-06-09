using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Validators.Email;

using FluentValidation;

namespace Altinn.Notifications.Validators.Orders;

/// <summary>
/// Represents validation rules for a request to send an email notification immediately to a single recipient.
/// </summary>
internal sealed class InstantEmailNotificationOrderRequestValidator : AbstractValidator<InstantEmailNotificationOrderRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstantEmailNotificationOrderRequestValidator"/> class.
    /// </summary>
    public InstantEmailNotificationOrderRequestValidator()
    {
        RuleFor(request => request.IdempotencyId)
            .NotEmpty()
            .WithMessage("IdempotencyId cannot be null or empty.");

        RuleFor(request => request.InstantEmailDetails)
            .NotNull()
            .WithMessage("Email details cannot be null.")
            .SetValidator(new InstantEmailDetailsValidator());
    }
}
