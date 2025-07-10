using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Validators.Recipient;

using FluentValidation;

namespace Altinn.Notifications.Validators.Orders;

/// <summary>
/// Represents validation rules for a request to send a notification immediately to a single recipient.
/// </summary>
internal sealed class InstantNotificationOrderRequestValidator : AbstractValidator<InstantNotificationOrderRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstantNotificationOrderRequestValidator"/> class.
    /// </summary>
    public InstantNotificationOrderRequestValidator()
    {
        RuleFor(request => request)
            .NotNull()
            .WithMessage("Notification order request cannot be null.")
            .DependentRules(() =>
            {
                RuleFor(request => request.IdempotencyId)
                    .NotEmpty()
                    .WithMessage("IdempotencyId cannot be null or empty.");

                RuleFor(request => request.Recipient)
                    .NotNull()
                    .WithMessage("Recipient information cannot be null.")
                    .SetValidator(new InstantNotificationRecipientValidator());
            });
    }
}
