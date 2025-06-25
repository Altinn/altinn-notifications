namespace Altinn.Notifications.Core.Models.SendCondition;

/// <summary>
/// Represents the outcome of evaluating a sending condition.
/// It is tracking the actual result of the sending condition check and whether a retry is needed.
/// </summary>
public record SendConditionEvaluationResult
{
    /// <summary>
    /// Gets the result of evaluating the sending condition.
    /// </summary>
    /// <remarks>
    /// A value of <c>true</c> means the sending criteria were met.
    /// A value of <c>false</c> means the sending criteria were not met.
    /// A <c>null</c> value indicates that the condition could not be evaluated.
    /// </remarks>
    public bool? IsSendingConditionMet { get; init; }

    /// <summary>
    /// Gets a value indicating whether the sending condition should be evaluated again.
    /// </summary>
    /// <remarks>
    /// A value of <c>true</c> indicates that the previous evaluation attempt
    /// encountered an issue, and a retry is needed after an appropriate delay.
    /// 
    /// A value of <c>false</c> indicates that no retry is necessary, either because the 
    /// condition was successfully evaluated or because the failure was non-transient.
    /// </remarks>
    public bool IsRetryNeeded { get; init; }
}
