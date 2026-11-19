namespace Altinn.Notifications.Core.Models.SendCondition;

/// <summary>
/// Represents the outcome of evaluating a sending condition.
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
    public bool? IsSendConditionMet { get; init; }
}
