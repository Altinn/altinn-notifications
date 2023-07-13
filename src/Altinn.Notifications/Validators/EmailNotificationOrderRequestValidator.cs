using Altinn.Notifications.Models;

using FluentValidation;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Claass contining validation logic for the <see cref="EmailNotificationOrderRequestExt"/> model
/// </summary>
public class EmailNotificationOrderRequestValidator : AbstractValidator<EmailNotificationOrderRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationOrderRequestValidator"/> class.
    /// </summary>
    public EmailNotificationOrderRequestValidator()
    {
        RuleFor(order => order.Recipients)
            .Empty()
            .When(order => order.ToAddresses != null && order.ToAddresses.Any())
            .WithMessage("Provide either recipients or to addresses, not both.");

        RuleFor(order => order.Recipients)
            .Must(recipients => recipients?.Exists(a => string.IsNullOrEmpty(a.EmailAddress)) == false)
            .When(o => o.Recipients != null)
            .WithMessage("Email address must be provided for all recipients.");

        RuleFor(order => order.SendTime)
          .Must(sendTime => sendTime >= DateTime.UtcNow.AddMinutes(-5))
          .WithMessage("Send time must be in the future. Leave blank to send immediatly.");

        RuleFor(order => order.Body).NotEmpty();
        RuleFor(order => order.Subject).NotEmpty();
        RuleFor(order => order.FromAddress).NotEmpty();
    }
}