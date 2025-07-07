using Altinn.Notifications.Models.Sms;

using FluentValidation;

namespace Altinn.Notifications.Validators.Sms;

/// <summary>
/// Represents validation logic for an SMS.
/// </summary>
internal sealed class SmsDetailsValidator : AbstractValidator<SmsDetailsExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsDetailsValidator"/> class.
    /// </summary>
    public SmsDetailsValidator()
    {
        RuleFor(sms => sms.Body)
            .NotEmpty()
            .WithMessage("SMS message body cannot be null or empty.");
    }
}
