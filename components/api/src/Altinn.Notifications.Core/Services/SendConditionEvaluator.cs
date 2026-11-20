using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.SendCondition;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of <see cref="ISendConditionEvaluator"/>.
/// </summary>
public class SendConditionEvaluator(IConditionClient conditionClient, ILogger<SendConditionEvaluator> logger) : ISendConditionEvaluator
{
    /// <inheritdoc/>
    public async Task<SendConditionEvaluationResult> Evaluate(Uri conditionEndpoint, Guid notificationId, bool isRetry)
    {
        var evaluationResult = await conditionClient.CheckSendCondition(conditionEndpoint);

        if (evaluationResult.IsSuccess)
        {
            logger.LogInformation(
                "// SendConditionEvaluator // Evaluate // Condition check yielded {ConditionMet} for notification '{NotificationId}' at endpoint '{Endpoint}'.",
                evaluationResult.Value,
                notificationId,
                conditionEndpoint);

            return new SendConditionEvaluationResult { IsSendConditionMet = evaluationResult.Value };
        }

        logger.LogInformation(
            "// SendConditionEvaluator // Evaluate // Condition check failed for notification '{NotificationId}' at endpoint '{Endpoint}'. Retry: {IsRetry}. Status code: {StatusCode}. Error message: '{ErrorMessage}'.",
            notificationId,
            conditionEndpoint,
            isRetry,
            evaluationResult.Error!.StatusCode,
            evaluationResult.Error.Message ?? "No error message provided");

        // A failed retry is treated as met so the notification is not held back indefinitely.
        return new SendConditionEvaluationResult { IsSendConditionMet = isRetry ? true : null };
    }
}
