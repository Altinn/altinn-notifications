using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Rules;

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
        RuleFor(order => order)
           .ValidateBaseProps();

        RuleFor(order => order)
            .ValidateEmailTemplateContent();

        RuleFor(order => order.Recipients)
            .ValidateRecipients();

        RuleFor(order => order.Recipients)
        .ChildRules(recipients =>
        {
            recipients.RuleForEach(recipient => recipient)
                .ChildRules(recipient =>
                {
                    recipient.RuleFor(r => r)
                   .Must(r => !string.IsNullOrEmpty(r.EmailAddress) || !string.IsNullOrEmpty(r.OrganizationNumber) || !string.IsNullOrEmpty(r.NationalIdentityNumber))
                   .WithMessage("Either a valid email address, organization number, or national identity number must be provided for each recipient.");
                });
        });
    }
}
