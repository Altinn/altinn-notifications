namespace Altinn.Notifications.Shared.TestInfrastructure.Utils;

/// <summary>
/// Provides static utility methods for wait/retry patterns commonly used in integration tests and infrastructure setup.
/// All methods use non-blocking Task.Delay between attempts.
/// </summary>
public static class WaitForUtils
{
    /// <summary>
    /// Executes an asynchronous predicate repeatedly until it returns true or max attempts is reached.
    /// </summary>
    /// <param name="predicate">The async predicate to check. Should return true when the condition is met.</param>
    /// <param name="maxAttempts">Maximum number of attempts before giving up.</param>
    /// <param name="delayMs">Delay in milliseconds between attempts.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>True if the predicate succeeded within the attempt limit, false otherwise.</returns>
    public static async Task<bool> WaitForAsync(
        Func<Task<bool>> predicate,
        int maxAttempts,
        int delayMs,
        CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (await predicate())
            {
                return true;
            }

            if (attempt < maxAttempts - 1)
            {
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        return false;
    }

    /// <summary>
    /// Executes an asynchronous operation repeatedly until it returns a non-null value or max attempts is reached.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute. Should return null to indicate the condition is not yet met.</param>
    /// <param name="maxAttempts">Maximum number of attempts before giving up.</param>
    /// <param name="delayMs">Delay in milliseconds between attempts.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The result of the operation, or null if all attempts returned null.</returns>
    public static async Task<T?> WaitForAsync<T>(
        Func<Task<T?>> operation,
        int maxAttempts,
        int delayMs,
        CancellationToken cancellationToken = default)
        where T : class
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var result = await operation();
            if (result != null)
            {
                return result;
            }

            if (attempt < maxAttempts - 1)
            {
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        return null;
    }
}
