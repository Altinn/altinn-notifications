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
    /// Repeatedly evaluates a condition until it becomes <c>true</c> or a timeout is reached.
    /// </summary>
    /// <param name="predicate">A function that evaluates the condition to be met. Returns <c>true</c> if the condition is satisfied, otherwise <c>false</c>.</param>
    /// <param name="maximumWaitTime">The maximum amount of time to wait for the condition to be met.</param>
    /// <param name="checkInterval">The interval between condition evaluations. Defaults to 100 milliseconds if not specified.</param>
    /// <returns>A task that completes when the condition is met or the timeout is reached.</returns>
    /// <exception cref="XunitException">Thrown if the condition is not met within the specified timeout.</exception>
    public static async Task EventuallyAsync(Func<bool> predicate, TimeSpan maximumWaitTime, TimeSpan? checkInterval = null)
    {
        var deadline = DateTime.UtcNow.Add(maximumWaitTime);
        var pollingInterval = checkInterval ?? TimeSpan.FromMilliseconds(100);

        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(pollingInterval);
        }

        throw new XunitException($"Condition not met within timeout ({maximumWaitTime}).");
    }
}
