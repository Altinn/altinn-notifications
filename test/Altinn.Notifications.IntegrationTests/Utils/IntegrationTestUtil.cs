using Xunit.Sdk;

namespace Altinn.Notifications.IntegrationTests.Utils;

public static class IntegrationTestUtil
{
    /// <summary>
    /// Repeatedly evaluates an async condition until it becomes <c>true</c> or a timeout is reached.
    /// Use this overload when your condition requires async operations (database queries, HTTP calls, file I/O, etc.).
    /// </summary>
    /// <param name="predicate">An async function that evaluates the condition to be met. Returns <c>true</c> if the condition is satisfied, otherwise <c>false</c>.</param>
    /// <param name="maximumWaitTime">The maximum amount of time to wait for the condition to be met.</param>
    /// <param name="checkInterval">The interval between condition evaluations. Defaults to 100 milliseconds if not specified.</param>
    /// <exception cref="XunitException">Thrown if the condition is not met within the specified timeout.</exception>
    public static async Task EventuallyAsync(Func<Task<bool>> predicate, TimeSpan maximumWaitTime, TimeSpan? checkInterval = null)
    {
        var deadline = DateTime.UtcNow.Add(maximumWaitTime);
        var pollingInterval = checkInterval ?? TimeSpan.FromMilliseconds(100);

        while (DateTime.UtcNow < deadline)
        {
            if (await predicate())
            {
                return;
            }

            await Task.Delay(pollingInterval);
        }

        throw new XunitException($"Condition not met within timeout ({maximumWaitTime}).");
    }

    /// <summary>
    /// Repeatedly evaluates a synchronous condition until it becomes <c>true</c> or a timeout is reached.
    /// Use this overload when your condition is purely synchronous (checking variables, object states, etc.).
    /// </summary>
    /// <param name="predicate">A synchronous function that evaluates the condition to be met. Returns <c>true</c> if the condition is satisfied, otherwise <c>false</c>.</param>
    /// <param name="maximumWaitTime">The maximum amount of time to wait for the condition to be met.</param>
    /// <param name="checkInterval">The interval between condition evaluations. Defaults to 100 milliseconds if not specified.</param>
    /// <returns>A task that completes when the condition is met or the timeout is reached.</returns>
    /// <exception cref="XunitException">Thrown if the condition is not met within the specified timeout.</exception>
    public static Task EventuallyAsync(Func<bool> predicate, TimeSpan maximumWaitTime, TimeSpan? checkInterval = null)
    {
        return EventuallyAsync(() => Task.FromResult(predicate()), maximumWaitTime, checkInterval);
    }
}
