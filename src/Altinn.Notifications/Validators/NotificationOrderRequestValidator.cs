using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Rules;

using FluentValidation;

using Microsoft.IdentityModel.Tokens;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Class containing validation logic for the <see cref="NotificationOrderRequestExt"/> model
/// </summary>
public class NotificationOrderRequestValidator : AbstractValidator<NotificationOrderRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationOrderRequestValidator"/> class.
    /// </summary>
    public NotificationOrderRequestValidator()
    {
        RuleFor(order => order)
            .ValidateBaseProps();

        RuleFor(order => order.NotificationChannel)
            .NotNull()
            .WithMessage("A notification channel must be defined");

        RuleFor(order => order)
               .ValidateOptionalEmailTemplate();

        RuleFor(order => order)
        .ValidateOptionalSmsTemplate();

        RuleFor(order => order.Recipients)
            .ChildRules(recipients =>
            {
                recipients.RuleForEach(recipient => recipient)
                    .ChildRules(recipient =>
                    {
                        recipient.RuleFor(r => r)
                            .Must(r => !string.IsNullOrEmpty(r.EmailAddress) ||
                                !string.IsNullOrEmpty(r.MobileNumber) ||
                                !string.IsNullOrEmpty(r.OrganizationNumber) ||
                                !string.IsNullOrEmpty(r.NationalIdentityNumber))
                            .WithMessage("Either a valid email address, mobile number starting with country code, organization number, or national identity number must be provided for each recipient.");
                    });
            });
    }
}
