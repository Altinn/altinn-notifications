using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models.SendCondition;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Extensions;

/// <summary>
/// Extension methods for <see cref="IConditionClient"/>.
/// </summary>
public static class ConditionClientExtensions
{
    /// <summary>
    /// Checks a send condition and limits the check to the given timeout.
    /// </summary>
    /// <param name="conditionClient">The condition client.</param>
    /// <param name="endpoint">The condition endpoint to check.</param>
    /// <param name="timeout">The maximum time the check may take.</param>
    /// <param name="cancellationToken">The cancellation token to observe, typically signalled on shutdown.</param>
    /// <returns>The send condition result, or a <see cref="ConditionClientError"/> if the check failed or timed out.</returns>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> was cancelled during the check.</exception>
    public static async Task<Result<bool, ConditionClientError>> CheckSendConditionWithTimeout(
        this IConditionClient conditionClient,
        Uri endpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var result = await conditionClient.CheckSendCondition(endpoint, timeoutCts.Token);

        // A cancellation caused by shutdown must not be mistaken for a timed out condition check.
        cancellationToken.ThrowIfCancellationRequested();

        return result;
    }
}
