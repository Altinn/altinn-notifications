using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Validators.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators.Orders;

/// <summary>
/// Represents validation rules for a request to send an SMS notification immediately to a single recipient.
/// </summary>
internal sealed class InstantSmsNotificationOrderRequestValidator : AbstractValidator<InstantSmsNotificationOrderRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstantSmsNotificationOrderRequestValidator"/> class.
    /// </summary>
    public InstantSmsNotificationOrderRequestValidator()
    {
        RuleFor(request => request.IdempotencyId)
            .NotEmpty()
            .WithMessage("IdempotencyId cannot be null or empty.");

        RuleFor(request => request.RecipientSms)
            .NotNull()
            .WithMessage("SMS details cannot be null.")
            .SetValidator(new ShortMessageDeliveryDetailsValidator());
    }
}
