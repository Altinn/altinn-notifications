using Altinn.Notifications.Extensions;
using Altinn.Notifications.Models;

using FluentValidation;

namespace Altinn.Notifications.Validators.Rules;

/// <summary>
/// Provides validation rules for the base properties of a notification order request.
/// </summary>
public static class NotificationOrderRequestBaseRules
{
    /// <summary>
    /// Validates the base properties of a notification order request.
    /// </summary>
    /// <typeparam name="T">The type of the notification order request.</typeparam>
    /// <param name="ruleBuilder">The rule builder.</param>
    /// <returns>The rule builder options.</returns>
    public static IRuleBuilderOptions<T, NotificationOrderRequestBaseExt> ValidateBaseProps<T>(this IRuleBuilder<T, NotificationOrderRequestBaseExt> ruleBuilder)
    where T : NotificationOrderRequestBaseExt
    {
        return ruleBuilder
            .ChildRules(order =>
            {
                order.RuleFor(o => o.RequestedSendTime)
                    .Must(sendTime => sendTime.Kind != DateTimeKind.Unspecified)
                    .WithMessage("The requested send time value must have specified a time zone.")
                    .Must(sendTime => sendTime >= DateTime.UtcNow.AddMinutes(-5))
                    .WithMessage("Send time must be in the future. Leave blank to send immediately.");

                order.RuleFor(o => o.ConditionEndpoint)
                    .Must(uri => uri == null || uri.IsValidUrl())
                    .WithMessage("The condition endpoint must be a valid URL.");
            });
    }
}
