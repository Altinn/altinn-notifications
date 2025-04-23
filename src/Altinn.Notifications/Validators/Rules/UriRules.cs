using Altinn.Notifications.Models;
using FluentValidation;

namespace Altinn.Notifications.Validators.Rules
{
    /// <summary>
    /// This class contains validation rules for URIs used in notification order requests.
    /// </summary>
    public static class UriRules
    {
        private static readonly string[] _protocolArray = ["https", "http"];

        /// <summary>
        /// Validates the condition endpoint of the notification reminder.
        /// </summary>
        /// <typeparam name="T">Class with properties for validation</typeparam>
        /// <param name="ruleBuilder">The ruleBuilder for chaining validation checks</param>
        /// <returns>Rulebuilder object</returns>
        public static IRuleBuilderOptions<T, Uri> ValidateConditionEndpoint<T>(this IRuleBuilderInitial<T, Uri> ruleBuilder)
            where T : class
        {
            return ruleBuilder.ChildRules(uri =>
            {
                uri.RuleFor(x => x)
                    .Must(x => x.IsAbsoluteUri && Uri.IsWellFormedUriString(x.ToString(), UriKind.Absolute))
                    .WithMessage("ConditionEndpoint must be a valid absolute URI or null.");

                uri.RuleFor(x => x)
                    .Must(conditionEndpoint => _protocolArray.Contains(conditionEndpoint.Scheme.ToLower()))
                    .When(x => x != null && x.IsAbsoluteUri)
                    .WithMessage("ConditionEndpoint must use http or https scheme.");
            });
        }
    }
}
