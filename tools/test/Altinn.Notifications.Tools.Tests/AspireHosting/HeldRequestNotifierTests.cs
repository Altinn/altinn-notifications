using System.Net;
using System.Text;

using Altinn.Notifications.Aspire.Hosting;

using Aspire.Hosting;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

using static Altinn.Notifications.Aspire.Hosting.HeldRequestNotifier;

namespace Altinn.Notifications.Tools.Tests.AspireHosting;

/// <summary>
/// The dashboard notifications for requests the control panel holds.
/// </summary>
public class HeldRequestNotifierTests
{
    private static readonly Uri _controlPanel = new("http://localhost:5096");

    private readonly Mock<IInteractionService> _interactions = new();
    private readonly Dictionary<string, CancellationToken> _shown = [];

    public HeldRequestNotifierTests()
    {
        _interactions.Setup(i => i.IsAvailable).Returns(true);
        _interactions
            .Setup(i => i.PromptNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationInteractionOptions>(), It.IsAny<CancellationToken>()))
            .Returns((string title, string message, NotificationInteractionOptions? options, CancellationToken cancellationToken) =>
            {
                lock (_shown)
                {
                    _shown[message] = cancellationToken;
                }

                // Stays up until the notifier takes it away, like a notification nobody dismisses.
                return new TaskCompletionSource<InteractionResult<bool>>().Task;
            });
    }

    [Fact]
    public void Update_ShowsEachNewHeldRequestOnce_AndTakesAwayTheOnesNoLongerHeld()
    {
        var notifier = new HeldRequestNotifier(new HttpClient(), _controlPanel, _interactions.Object, NullLogger.Instance);
        var acs = Held("1", "acs", "/emails:send");
        var profile = Held("2", "profile", "/profile/api/v1/users");

        notifier.Update([acs]);
        notifier.Update([acs, profile]);
        notifier.Update([profile]);

        Assert.Equal(2, _shown.Count);
        Assert.True(Shown(acs).IsCancellationRequested);
        Assert.False(Shown(profile).IsCancellationRequested);
        _interactions.Verify(
            i => i.PromptNotificationAsync("Held acs request", It.IsAny<string>(), It.Is<NotificationInteractionOptions>(o => o.LinkUrl == "http://localhost:5096/#requests"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_ShowsWhatTheControlPanelHolds_AndTakesItAwayWhenTheStreamEnds()
    {
        using var stopped = new CancellationTokenSource();
        var acs = Held("1", "acs", "/emails:send");
        var handler = new ControlPanelHandler(
            events: "data: changed\n\n",
            state: """{"intercept":true,"pending":[{"id":"1","system":"acs","method":"POST","path":"/emails:send","expiresAt":"2026-10-26T12:00:00Z"}]}""",
            onStreamEnd: stopped.Cancel);
        var notifier = new HeldRequestNotifier(new HttpClient(handler), _controlPanel, _interactions.Object, NullLogger.Instance);

        await notifier.RunAsync(stopped.Token);

        Assert.Single(_shown);
        Assert.True(Shown(acs).IsCancellationRequested);
    }

    [Fact]
    public async Task RunAsync_WithoutDashboard_DoesNothing()
    {
        _interactions.Setup(i => i.IsAvailable).Returns(false);
        var notifier = new HeldRequestNotifier(new HttpClient(), _controlPanel, _interactions.Object, NullLogger.Instance);

        await notifier.RunAsync(CancellationToken.None);

        Assert.Empty(_shown);
    }

    private static HeldRequest Held(string id, string system, string path) =>
        new(id, system, "POST", path, new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));

    private CancellationToken Shown(HeldRequest request) =>
        _shown.Single(s => s.Key.StartsWith($"{request.Method} {request.Path} ", StringComparison.Ordinal)).Value;

    /// <summary>
    /// Serves one event stream that ends after <paramref name="events"/>, and <paramref name="state"/> for <c>/api/state</c>.
    /// </summary>
    private sealed class ControlPanelHandler(string events, string state, Action onStreamEnd) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            return request.RequestUri!.AbsolutePath switch
            {
                "/api/events" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new EndingStream(Encoding.UTF8.GetBytes(events), onStreamEnd)) },
                "/api/state" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(state, Encoding.UTF8, "application/json") },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }
    }

    /// <summary>
    /// A stream that calls <paramref name="onEnd"/> when it has been read to the end, like a control panel going away.
    /// </summary>
    private sealed class EndingStream(byte[] content, Action onEnd) : MemoryStream(content)
    {
        public override int Read(byte[] buffer, int offset, int count) => Ended(base.Read(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Ended(await base.ReadAsync(buffer, cancellationToken));

        private int Ended(int read)
        {
            if (read == 0)
            {
                onEnd();
            }

            return read;
        }
    }
}
