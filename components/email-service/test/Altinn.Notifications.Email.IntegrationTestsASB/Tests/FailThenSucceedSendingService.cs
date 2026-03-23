using Altinn.Notifications.Email.Core.Sending;
using Altinn.Notifications.Email.Integrations.Wolverine;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Tests;

/// <summary>
/// Test double for <see cref="ISendingService"/> that throws <see cref="InvalidOperationException"/>
/// for the first <paramref name="failCount"/> invocations, then succeeds.
/// Used to verify that the Wolverine retry policy configured in
/// <see cref="SendEmailCommandHandler.Configure"/> retries transient failures.
/// </summary>
/// <param name="failCount">The number of initial calls that should fail before succeeding.</param>
internal sealed class FailThenSucceedSendingService(int failCount) : ISendingService
{
    private int _attempts;
    private readonly TaskCompletionSource<Core.Sending.Email> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Gets the total number of times <see cref="SendAsync"/> has been invoked.
    /// </summary>
    public int AttemptCount => _attempts;

    /// <summary>
    /// Simulates sending an email. Throws <see cref="InvalidOperationException"/> for the first calls, then signals success.
    /// </summary>
    /// <param name="email">The email to send.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the current attempt number is less than or equal to failCount parameter.
    /// </exception>
    public Task SendAsync(Core.Sending.Email email)
    {
        var attempt = Interlocked.Increment(ref _attempts);

        if (attempt <= failCount)
        {
            throw new InvalidOperationException($"Transient failure (attempt {attempt})");
        }

        _tcs.TrySetResult(email);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Waits until <see cref="SendAsync"/> succeeds and returns the captured email,
    /// or returns <see langword="null"/> if the <paramref name="timeout"/> elapses first.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for a successful send.</param>
    /// <returns>The captured <see cref="Core.Sending.Email"/>, or <see langword="null"/> on timeout.</returns>
    public async Task<Core.Sending.Email?> WaitForEmailAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            return await _tcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
