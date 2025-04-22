using Altinn.Notifications.Models;
using FluentValidation;

namespace Altinn.Notifications.Validators.Rules
{
    /// <summary>
    /// This class contains validation rules for URIs used in notification order requests.
    /// </summary>
    public static class UriRules
    {
        /// <summary>
        /// Validates the condition endpoint of the notification reminder.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ruleBuilder">The ruleBuilder for chaining validation checks</param>
        /// <returns></returns>
        public static IRuleBuilderOptions<T, Uri> ValidateConditionEndpoint<T>(this IRuleBuilderInitial<T, Uri> ruleBuilder)
            where T : class
        {
            return ruleBuilder.ChildRules(uri =>
            {
                uri.RuleFor(x => x)
                    .Must(BeAbsoluteUri!)
                    .WithMessage("ConditionEndpoint must be a valid absolute URI or null.");

                uri.RuleFor(x => x)
                    .Must(conditionEndpoint => conditionEndpoint == null || Uri.IsWellFormedUriString(conditionEndpoint.ToString(), UriKind.Absolute))
                    .WithMessage("ConditionEndpoint must be a valid absolute URI or null.");

                uri.RuleFor(x => x)
                    .Must(conditionEndpoint => new string[] { "https", "http" }.Contains(conditionEndpoint!.Scheme.ToLower()))
                    .When(x => x.IsAbsoluteUri)
                    .WithMessage("ConditionEndpoint must use http or https scheme.");
            });
        }

        private static bool BeAbsoluteUri(Uri uri)
        {
            return uri.IsAbsoluteUri;
        }
    }
}
