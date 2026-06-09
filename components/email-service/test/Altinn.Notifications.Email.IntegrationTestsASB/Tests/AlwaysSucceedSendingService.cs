using Altinn.Notifications.Email.Core.Sending;

namespace Altinn.Notifications.Email.IntegrationTestsASB.Tests;

/// <summary>
/// Test double for <see cref="ISendingService"/> that always succeeds immediately,
/// capturing the email for later assertion via <see cref="CapturedEmail"/>
/// or <see cref="WaitForEmailAsync"/>.
/// </summary>
internal sealed class AlwaysSucceedSendingService : ISendingService
{
    private readonly TaskCompletionSource<Core.Sending.Email> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Gets the most recently sent email, or <see langword="null"/> if <see cref="SendAsync"/> has not been called.
    /// </summary>
    public Core.Sending.Email? CapturedEmail { get; private set; }

    /// <summary>
    /// Captures <paramref name="email"/> and signals completion to any pending <see cref="WaitForEmailAsync"/> call.
    /// </summary>
    /// <param name="email">The email to send.</param>
    public Task SendAsync(Core.Sending.Email email)
    {
        CapturedEmail = email;
        _tcs.TrySetResult(email);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Waits until <see cref="SendAsync"/> is called and returns the captured email,
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
