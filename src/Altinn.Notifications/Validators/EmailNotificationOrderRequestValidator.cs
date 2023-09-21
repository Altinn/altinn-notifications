using Altinn.Notifications.Models;

using FluentValidation;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Class containing validation logic for the <see cref="EmailNotificationOrderRequestExt"/> model
/// </summary>
public class EmailNotificationOrderRequestValidator : AbstractValidator<EmailNotificationOrderRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationOrderRequestValidator"/> class.
    /// </summary>
    public EmailNotificationOrderRequestValidator()
    {
        RuleFor(order => order.Recipients)
            .NotEmpty()
            .WithMessage("One or more recipient is required.")
            .Must(recipients => recipients?.Exists(a => string.IsNullOrEmpty(a.EmailAddress)) == false)
            .WithMessage("Email address must be provided for all recipients.");

        RuleFor(order => order.RequestedSendTime)
          .Must(sendTime => sendTime >= DateTime.UtcNow.AddMinutes(-5))
          .WithMessage("Send time must be in the future. Leave blank to send immediately.");

        RuleFor(order => order.Body).NotEmpty();
        RuleFor(order => order.Subject).NotEmpty();
        RuleFor(order => order.FromAddress).NotEmpty();
    }
}
