using Altinn.Notifications.Models.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators.Sms;

/// <summary>
/// Represents validation logic for SMS body content and sender identifier.
/// </summary>
internal sealed class SmsDetailsValidator : AbstractValidator<SmsDetailsExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsDetailsValidator"/> class.
    /// </summary>
    public SmsDetailsValidator()
    {
        RuleFor(smsDetails => smsDetails)
            .NotNull()
            .WithMessage("SMS details cannot be null.")
            .DependentRules(() =>
            {
                RuleFor(smsDetails => smsDetails.Body)
                    .NotEmpty()
                    .WithMessage("SMS message body cannot be null or empty.");
            });
    }
}
