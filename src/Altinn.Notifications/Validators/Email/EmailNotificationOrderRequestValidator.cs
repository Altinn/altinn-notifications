using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Rules;

using FluentValidation;

namespace Altinn.Notifications.Validators.Email;

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
        RuleFor(order => order)
           .ValidateBaseProps();

        RuleFor(order => order)
            .ValidateEmailTemplateContent();

        RuleFor(order => order.Recipients)
            .ValidateEmailRecipients();
    }
}
