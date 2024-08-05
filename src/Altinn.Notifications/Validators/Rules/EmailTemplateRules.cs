using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;

using FluentValidation;

namespace Altinn.Notifications.Validators.Rules
{
    /// <summary>
    /// Provides validation rules for email templates used in notification order requests.
    /// This class contains methods to validate the presence and content of email templates
    /// based on the notification channel and specific properties of the email template.
    /// </summary>
    public static class EmailTemplateRules
    {
        private static readonly NotificationChannelExt[] _emailTemplateRequiredChannels =
        [
            NotificationChannelExt.Email,
            NotificationChannelExt.EmailPreferred,
            NotificationChannelExt.SmsPreferred
        ];

        /// <summary>
        /// Validates the email template for the specified notification order request.
        /// </summary>
        /// <typeparam name="T">The type of the notification order request.</typeparam>
        /// <param name="ruleBuilder">The rule builder.</param>
        /// <returns>The rule builder options.</returns>
        public static IRuleBuilderOptions<T, T> ValidateOptionalEmailTemplate<T>(this IRuleBuilderInitial<T, T> ruleBuilder)
            where T : NotificationOrderRequestExt
        {
            return ruleBuilder
                .ChildRules(order =>
                {
                    order.RuleFor(o => o.EmailTemplate)
                       .NotNull()
                       .When(order => _emailTemplateRequiredChannels.Contains(order.NotificationChannel!.Value))
                       .WithMessage("An email template is required for the selected the notification channel.")
                       .DependentRules(() =>
                       {
                           ApplyCommonEmailTemplateRules(order.RuleFor(o => o.EmailTemplate!));
                       });
                });
        }

        /// <summary>
        /// Validates the email template properties for the specified email notification order request.
        /// </summary>
        /// <typeparam name="T">The type of the email notification order request.</typeparam>
        /// <param name="ruleBuilder">The rule builder.</param>
        /// <returns>The rule builder options.</returns>
        public static IRuleBuilderOptions<T, T> ValidateEmailTemplateContent<T>(this IRuleBuilderInitial<T, T> ruleBuilder)
            where T : EmailNotificationOrderRequestExt
        {
            return ruleBuilder
                .ChildRules(order =>
                {
                    order.RuleFor(order => order.ToEmailTemplateExt())
                      .ChildRules(template =>
                      {
                          ApplyCommonEmailTemplateRules(template.RuleFor(t => t));
                      });
                });
        }

        /// <summary>
        /// Applies common validation rules for the Sms template.
        /// </summary>
        /// <typeparam name="T">The type of the template.</typeparam>
        /// <param name="ruleBuilder">The rule builder for the template.</param>
        private static void ApplyCommonEmailTemplateRules<T>(IRuleBuilderInitial<T, EmailTemplateExt> ruleBuilder)
        {
            ruleBuilder
                .ChildRules(template =>
                {
                    template.RuleFor(t => t.Body)
                        .NotEmpty()
                        .WithMessage("The email template body must not be empty.");

                    template.RuleFor(t => t.Subject)
                        .NotEmpty()
                        .WithMessage("The email template subject must not be empty.");
                });
        }
    }
}
