using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models;

using FluentValidation;

namespace Altinn.Notifications.Validators.Rules
{
    /// <summary>
    /// Provides validation rules for Sms templates used in notification order requests.
    /// This class contains methods to validate the presence and content of Sms templates
    /// based on the notification channel and specific properties of the Sms template.
    /// </summary>
    public static class SmsTemplateRules
    {
        private static readonly NotificationChannelExt[] _smsTemplateRequiredChannels =
        [
            NotificationChannelExt.Sms,
            NotificationChannelExt.SmsPreferred,
            NotificationChannelExt.EmailPreferred
        ];

        /// <summary>
        /// Validates the Sms template for the specified notification order request.
        /// </summary>
        /// <typeparam name="T">The type of the notification order request.</typeparam>
        /// <param name="ruleBuilder">The rule builder.</param>
        /// <returns>The rule builder options.</returns>
        public static IRuleBuilderOptions<T, T> ValidateOptionalSmsTemplate<T>(this IRuleBuilderInitial<T, T> ruleBuilder)
            where T : NotificationOrderRequestExt
        {
            return ruleBuilder
                .ChildRules(order =>
                {
                    order.RuleFor(o => o.SmsTemplate)
                       .NotNull()
                       .When(order => _smsTemplateRequiredChannels.Contains(order.NotificationChannel!.Value))
                       .WithMessage("An sms template is required for the selected the notification channel.")
                       .DependentRules(() =>
                       {
                           ApplyCommonSmsTemplateRules(order.RuleFor(o => o.SmsTemplate!));
                       });
                });
        }

        /// <summary>
        /// Validates the Sms template properties for the specified Sms notification order request.
        /// </summary>
        /// <typeparam name="T">The type of the Sms notification order request.</typeparam>
        /// <param name="ruleBuilder">The rule builder.</param>
        /// <returns>The rule builder options.</returns>
        public static IRuleBuilderOptions<T, T> ValidateSmsTemplateContent<T>(this IRuleBuilderInitial<T, T> ruleBuilder)
            where T : SmsNotificationOrderRequestExt
        {
            return ruleBuilder
                .ChildRules(order =>
                {                  
                    order.RuleFor(order => order.ToSmsTemplateExt())
                      .ChildRules(template =>
                      {
                          ApplyCommonSmsTemplateRules(template.RuleFor(t => t));
                      });
                });
        }

        /// <summary>
        /// Applies common validation rules for the Sms template.
        /// </summary>
        /// <typeparam name="T">The type of the template.</typeparam>
        /// <param name="ruleBuilder">The rule builder for the template.</param>
        private static void ApplyCommonSmsTemplateRules<T>(IRuleBuilderInitial<T, SmsTemplateExt> ruleBuilder)
        {
            ruleBuilder
                .ChildRules(template =>
                {
                    template.RuleFor(t => t.Body)
                        .NotEmpty()
                        .WithMessage("The sms template body must not be empty.");
                });
        }
    }
}
