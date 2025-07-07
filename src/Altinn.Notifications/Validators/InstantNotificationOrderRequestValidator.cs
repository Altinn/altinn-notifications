using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Validators.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators;

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
        RuleFor(request => request.IdempotencyId)
            .NotEmpty()
            .WithMessage("IdempotencyId cannot be null or empty.");

        RuleFor(request => request.Recipient)
            .NotEmpty()
            .WithMessage("Recipient information cannot be null or empty.");

        RuleFor(request => request.Recipient!.RecipientTimedSms)
            .NotNull()
            .SetValidator(new RecipientTimedSmsValidator());
    }
}
