using Altinn.Notifications.Models.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators.Sms;

/// <summary>
/// Represents validation logic for content and sender information for an SMS.
/// </summary>
internal sealed class ShortMessageContentValidator : AbstractValidator<ShortMessageContentExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShortMessageContentValidator"/> class.
    /// </summary>
    public ShortMessageContentValidator()
    {
        RuleFor(contents => contents)
            .NotNull()
            .WithMessage("SMS content cannot be null.");
    }
}
