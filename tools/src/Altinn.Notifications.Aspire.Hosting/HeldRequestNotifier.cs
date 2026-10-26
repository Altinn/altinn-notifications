using System.Net.Http.Json;

using Aspire.Hosting;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Aspire.Hosting;

/// <summary>
/// Shows a dashboard notification for each request the control panel holds, and takes it away when the request is answered, times
/// out or is cancelled. Answering stays in the control panel's GUI.
/// </summary>
/// <param name="client">A client without a timeout, since the control panel's event stream stays open.</param>
/// <param name="controlPanel">The control panel's base URL.</param>
/// <param name="interactions">The dashboard's notifications.</param>
/// <param name="logger">The control panel's log in the dashboard.</param>
internal sealed class HeldRequestNotifier(HttpClient client, Uri controlPanel, IInteractionService interactions, ILogger logger)
{
    private static readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(2);

    private readonly Dictionary<string, CancellationTokenSource> _shown = [];

    /// <summary>
    /// Follows the control panel's event stream until <paramref name="cancellationToken"/> is cancelled, reconnecting when the
    /// control panel is not up yet or restarts.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!interactions.IsAvailable)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await FollowAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e) when (e is HttpRequestException or IOException)
            {
                // Not up yet, or restarted. Held requests do not survive a restart, so their notifications go.
                logger.LogDebug("Waiting for the control panel's event stream: {Message}", e.Message);
            }

            Update([]);

            try
            {
                await Task.Delay(_reconnectDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Update([]);
    }

    /// <summary>
    /// Shows a notification for each request in <paramref name="pending"/> that has none, and takes away the ones that are no longer held.
    /// </summary>
    internal void Update(IReadOnlyList<HeldRequest> pending)
    {
        foreach (string id in _shown.Keys.Except(pending.Select(r => r.Id)).ToList())
        {
            _shown.Remove(id, out var notification);
            notification!.Cancel();
            notification.Dispose();
        }

        foreach (var request in pending.Where(r => !_shown.ContainsKey(r.Id)))
        {
            var notification = new CancellationTokenSource();
            _shown[request.Id] = notification;
            _ = ShowAsync(request, notification.Token);
        }
    }

    private async Task FollowAsync(CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(new Uri(controlPanel, "/api/events"), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken));

        // Each event only says that something changed, so read the held requests again.
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var state = await client.GetFromJsonAsync<InterceptState>(new Uri(controlPanel, "/api/state"), cancellationToken);
                Update(state?.Pending ?? []);
            }
        }
    }

    private async Task ShowAsync(HeldRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await interactions.PromptNotificationAsync(
                $"Held {request.System} request",
                $"{request.Method} {request.Path} waits for an answer in the control panel until {request.ExpiresAt.ToLocalTime():HH:mm:ss}.",
                new NotificationInteractionOptions
                {
                    Intent = MessageIntent.Information,
                    LinkText = "Answer it",
                    LinkUrl = new Uri(controlPanel, "/#requests").ToString(),
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Answered, timed out or cancelled.
        }
    }

    /// <summary>
    /// The part of the control panel's <c>/api/state</c> this needs.
    /// </summary>
    internal sealed record InterceptState(IReadOnlyList<HeldRequest>? Pending);

    /// <summary>
    /// A request the control panel holds.
    /// </summary>
    internal sealed record HeldRequest(string Id, string System, string Method, string Path, DateTimeOffset ExpiresAt);
}
