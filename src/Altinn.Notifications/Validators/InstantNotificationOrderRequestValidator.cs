using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Validators.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Represents validation rules for the model that receives data from clients.
/// </summary>
internal sealed class InstantNotificationOrderRequestValidator : AbstractValidator<InstantNotificationOrderRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstantNotificationOrderRequestValidator"/> class.
    /// </summary>
    public InstantNotificationOrderRequestValidator()
    {
        RuleFor(request => request.IdempotencyId)
            .NotNull()
            .NotEmpty()
            .WithMessage("IdempotencyId cannot be null or empty.");

        RuleFor(request => request.Recipient!.RecipientSms)
            .NotNull()
            .SetValidator(new RecipientInstantSmsValidator());
    }
}
