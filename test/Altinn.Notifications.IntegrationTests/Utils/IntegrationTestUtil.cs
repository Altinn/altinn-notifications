using System.Diagnostics;

using Altinn.Notifications.Core.Models.Notification;

namespace Altinn.Notifications.IntegrationTests.Utils;

public static class IntegrationTestUtil
{
    private static readonly TimeSpan _defaultPollingInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Repeatedly evaluates an async condition until it becomes <c>true</c> or a timeout is reached.
    /// Use this overload when your condition requires async operations (database queries, HTTP calls, file I/O, etc.).
    /// </summary>
    /// <param name="predicate">An async function that evaluates the condition to be met. Returns <c>true</c> if the condition is satisfied, otherwise <c>false</c>.</param>
    /// <param name="maximumWaitTime">The maximum amount of time to wait for the condition to be met.</param>
    /// <param name="checkInterval">The interval between condition evaluations. Defaults to 100 milliseconds if not specified.</param>
    /// <param name="cancellationToken">Token to cancel waiting early.</param>
    public static async Task EventuallyAsync(Func<Task<bool>> predicate, TimeSpan maximumWaitTime, TimeSpan? checkInterval = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var pollingInterval = checkInterval ?? _defaultPollingInterval;

        int attempts = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts++;

            bool satisfied;
            try
            {
                satisfied = await predicate().ConfigureAwait(false);
            }
            catch (Exception)
            {
                satisfied = false;
            }

            if (satisfied)
            {
                return;
            }

            var remaining = maximumWaitTime - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var delay = remaining < pollingInterval ? remaining : pollingInterval;

            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
        }
    }

    /// <summary>
    /// Repeatedly evaluates a synchronous condition until it becomes <c>true</c> or a timeout is reached.
    /// Use this overload when your condition is purely synchronous (checking variables, object states, etc.).
    /// </summary>
    /// <param name="predicate">A synchronous function that evaluates the condition to be met. Returns <c>true</c> if the condition is satisfied, otherwise <c>false</c>.</param>
    /// <param name="maximumWaitTime">The maximum amount of time to wait for the condition to be met.</param>
    /// <param name="checkInterval">The interval between condition evaluations. Defaults to 100 milliseconds if not specified.</param>
    /// <param name="cancellationToken">Token to cancel waiting early.</param>
    /// <returns>A task that completes when the condition is met or the timeout is reached.</returns>
    public static Task EventuallyAsync(Func<bool> predicate, TimeSpan maximumWaitTime, TimeSpan? checkInterval = null, CancellationToken cancellationToken = default)
    {
        return EventuallyAsync(() => Task.FromResult(predicate()), maximumWaitTime, checkInterval, cancellationToken);
    }

    /// <summary>
    /// Polls the database to check if a notification has reached the expected result status.
    /// </summary>
    /// <param name="notification">The notification to check.</param>
    /// <param name="timeout">The maximum time to wait for the expected result. Defaults to 5 seconds.</param>
    /// <returns>Returns 1 if the notification reached 'Sending' status within the timeout period; otherwise, returns 0.</returns>
    public static async Task<int> PollSendingNotificationStatus<T>(T notification, TimeSpan? timeout = null)
        where T : class
    {
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(5);
        var stopwatch = Stopwatch.StartNew();

        string tableName;
        Guid notificationId;

        if (notification is EmailNotification emailNotification)
        {
            tableName = "emailnotifications";
            notificationId = emailNotification.Id;
        }
        else if (notification is SmsNotification smsNotification)
        {
            tableName = "smsnotifications";
            notificationId = smsNotification.Id;
        }
        else
        {
            return 0;
        }

        while (stopwatch.Elapsed < actualTimeout)
        {
            string sqlCheck = $"select count(1) from notifications.{tableName} where result = 'Sending' and alternateid='{notificationId}'";
            long count = await PostgreUtil.RunSqlReturnOutput<long>(sqlCheck);
            if (count == 1)
            {
                return 1;
            }

            await Task.Delay(20); // wait a bit before checking again
        }

        return 0;
    }
}
