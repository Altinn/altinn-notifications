using Altinn.Notifications.Extensions;
using Altinn.Notifications.Models;

using FluentValidation;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Represents validation logic for the notification order with reminders request model.
/// </summary>
public class NotificationOrderWithRemindersRequestValidator : AbstractValidator<NotificationOrderSequenceRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationOrderWithRemindersRequestValidator"/> class.
    /// </summary>
    public NotificationOrderWithRemindersRequestValidator()
    {
        // Validate RequestedSendTime.
        RuleFor(o => o.RequestedSendTime)
            .Must(sendTime => sendTime.Kind != DateTimeKind.Unspecified)
            .WithMessage("The requested send time value must have specified a time zone.")
            .Must(sendTime => sendTime >= DateTime.UtcNow.AddMinutes(-5))
            .WithMessage("Send time must be in the future. Leave blank to send immediately.");

        // Validate ConditionEndpoint.
        RuleFor(o => o.ConditionEndpoint)
            .Must(uri => uri == null || uri.IsValidUrl())
            .WithMessage("The condition endpoint must be a valid URL.");

        // Validate IdempotencyId.
        RuleFor(order => order.IdempotencyId)
            .NotEmpty()
            .WithMessage("Idempotency identifier is required.");
    }
}
