using Altinn.Notifications.Models.NotificationLog;

using FluentValidation;

namespace Altinn.Notifications.Validators.Log;

/// <summary>
/// Validates <see cref="NotificationLogQueryExt"/> query parameters for notification log lookups.
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
    }
}
