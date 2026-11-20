using Altinn.Notifications.Core.Models.SendCondition;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Evaluates the send condition of a single notification at send time.
/// </summary>
public interface ISendConditionEvaluator
{
    /// <summary>
    /// Calls the condition endpoint and determines whether the notification should be sent.
    /// </summary>
    /// <param name="conditionEndpoint">The condition endpoint to call.</param>
    /// <param name="notificationId">The notification the condition belongs to, used for logging.</param>
    /// <param name="isRetry">Whether this is the retry of an earlier inconclusive evaluation.</param>
    /// <returns>
    /// A result where <see cref="SendConditionEvaluationResult.IsSendConditionMet"/> is <c>true</c> if the notification should be sent,
    /// <c>false</c> if the condition is not met, and <c>null</c> if the endpoint could not be reached and the notification should be retried.
    /// When <paramref name="isRetry"/> is true, an unreachable endpoint yields <c>true</c>.
    /// </returns>
    Task<SendConditionEvaluationResult> Evaluate(Uri conditionEndpoint, Guid notificationId, bool isRetry);
}
