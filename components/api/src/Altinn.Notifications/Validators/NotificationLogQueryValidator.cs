using Altinn.Notifications.Models.NotificationLog;

using FluentValidation;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Validates <see cref="NotificationLogQueryExt"/> query parameters for notification log lookups.
/// At least one of <see cref="NotificationLogQueryExt.DialogId"/> or
/// <see cref="NotificationLogQueryExt.TransmissionId"/> must be provided and non-whitespace.
/// </summary>
internal sealed class NotificationLogQueryValidator : AbstractValidator<NotificationLogQueryExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationLogQueryValidator"/> class.
    /// </summary>
    public NotificationLogQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.DialogId) || !string.IsNullOrWhiteSpace(x.TransmissionId))
            .WithName("query")
            .WithMessage("At least one of 'dialogId' or 'transmissionId' must be provided.");

        When(x => !string.IsNullOrWhiteSpace(x.DialogId), () =>
        {
            RuleFor(x => x.DialogId!)
                .MaximumLength(255)
                .WithMessage("'dialogId' must not exceed 255 characters.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.TransmissionId), () =>
        {
            RuleFor(x => x.TransmissionId!)
                .MaximumLength(255)
                .WithMessage("'transmissionId' must not exceed 255 characters.");
        });
    }
}
