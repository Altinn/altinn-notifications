using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Rules;

using FluentValidation;

namespace Altinn.Notifications.Validators;

/// <summary>
/// Class containing validation logic for the <see cref="SmsNotificationOrderRequestExt"/> model
/// </summary>
public class SmsNotificationOrderRequestValidator : AbstractValidator<SmsNotificationOrderRequestExt>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNotificationOrderRequestValidator"/> class.
    /// </summary>
    public SmsNotificationOrderRequestValidator()
    {
        RuleFor(order => order)
            .ValidateBaseProps();

        RuleFor(order => order)
            .ValidateSmsTemplateContent();

        RuleFor(order => order.Recipients)
            .ValidateRecipients();

        RuleFor(order => order.Recipients)
          .ChildRules(recipients =>
          {
              recipients.RuleForEach(recipient => recipient)
                  .ChildRules(recipient =>
                  {
                      recipient.RuleFor(r => r)
                     .Must(r => !string.IsNullOrEmpty(r.MobileNumber) || !string.IsNullOrEmpty(r.OrganizationNumber) || !string.IsNullOrEmpty(r.NationalIdentityNumber))
                     .WithMessage("Either a valid mobile number starting with country code, organization number, or national identity number must be provided for each recipient.");
                  });
          });
    }
}
