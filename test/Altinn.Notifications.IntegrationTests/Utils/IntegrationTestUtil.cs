using Xunit.Sdk;

namespace Altinn.Notifications.IntegrationTests.Utils;

public static class IntegrationTestUtil
{
    /// <summary>
    /// Repeatedly evaluates a condition until it becomes <c>true</c> or a timeout is reached.
    /// </summary>
    /// <param name="predicate">An async function that evaluates the condition to be met. Returns <c>true</c> if the condition is satisfied, otherwise <c>false</c>.</param>
    /// <param name="maximumWaitTime">The maximum amount of time to wait for the condition to be met.</param>
    /// <param name="checkInterval">The interval between condition evaluations. Defaults to 100 milliseconds if not specified.</param>
    /// <exception cref="XunitException">Thrown if the condition is not met within the specified timeout.</exception>
    public static Task EventuallyAsync(Func<Task<bool>> predicate, TimeSpan maximumWaitTime, TimeSpan? checkInterval = null)
    {
        return EventuallyAsync<bool>(async () => await predicate(), maximumWaitTime, checkInterval ?? TimeSpan.FromMilliseconds(200));
    }

    /// <summary>
    /// Repeatedly evaluates a condition until it becomes <c>true</c> or a timeout is reached.
    /// </summary>
    /// <param name="predicate">A function that evaluates the condition to be met. Returns <c>true</c> if the condition is satisfied, otherwise <c>false</c>.</param>
    /// <param name="maximumWaitTime">The maximum amount of time to wait for the condition to be met.</param>
    /// <param name="checkInterval">The interval between condition evaluations. Defaults to 100 milliseconds if not specified.</param>
    /// <returns>A task that completes when the condition is met or the timeout is reached.</returns>
    /// <exception cref="XunitException">Thrown if the condition is not met within the specified timeout.</exception>
    public static Task EventuallyAsync(Func<bool> predicate, TimeSpan maximumWaitTime, TimeSpan? checkInterval = null)
    {
        return EventuallyAsync<bool>(() => Task.FromResult(predicate()), maximumWaitTime, checkInterval ?? TimeSpan.FromMilliseconds(200));
    }

    /// <summary>
    /// Repeatedly evaluates a condition until it returns a non-null, non-default value or a timeout is reached.
    /// </summary>
    /// <typeparam name="T">The type of the result returned by the predicate.</typeparam>
    /// <param name="predicate">An async function that evaluates and returns a value of type <typeparamref name="T"/>. The method continues polling until this returns a non-null, non-default value.</param>
    /// <param name="maximumWaitTime">The maximum amount of time to wait for a valid result.</param>
    /// <param name="checkInterval">The interval between condition evaluations.</param>
    /// <returns>A task that completes with the non-null, non-default result from the predicate.</returns>
    /// <exception cref="TimeoutException">Thrown if a valid result is not obtained within the specified timeout.</exception>
    public static async Task<T> EventuallyAsync<T>(
       Func<Task<T>> predicate,
       TimeSpan maximumWaitTime,
       TimeSpan checkInterval)
    {
        var endTime = DateTime.UtcNow.Add(maximumWaitTime);

        while (DateTime.UtcNow < endTime)
        {
            var result = await predicate();
            if (result != null && !EqualityComparer<T>.Default.Equals(result, default))
            {
                return result;
            }

            await Task.Delay(checkInterval);
        }

        throw new TimeoutException($"Condition was not met within {maximumWaitTime.TotalSeconds} seconds");
    }
}
