namespace Altinn.Notifications.Tools.TestHarness.Mirror;

/// <summary>
/// An SMS as <c>wiremock-linkmobility</c> received it: LinkMobility's <c>TTL</c> in minutes, the gateway reference the mock answered
/// with (empty when it rejected the message), and the mock's scenario for the recipient, e.g. <c>Undelivered</c>.
/// </summary>
internal sealed record MirroredSms(string? Sender, string Recipient, string? Text, int? Ttl, string? Reference, string? Scenario);

/// <summary>
/// <c>/mirror/{channel}</c>, where the mocks send a copy of what they receive and its status, and <c>/api/mirror/{channel}</c>,
/// which the GUI's Phone tab reads. Read-only for the GUI: nothing here goes back to the mocks.
/// </summary>
internal static class MirrorEndpoints
{
    public static void MapMirrorEndpoints(this WebApplication app)
    {
        app.MapPost("/mirror/sms", AddSms);
        app.MapPost("/mirror/{channel}/{id}/status", UpdateStatus);

        app.MapGet("/api/mirror/{channel}", GetMessages);
        app.MapDelete("/api/mirror/{channel}", Clear);
    }

    private static IResult AddSms(SmsMirrorRequest request, MirrorStores stores)
    {
        if (string.IsNullOrWhiteSpace(request.Recipient))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["recipient"] = ["An SMS needs a recipient."] });
        }

        if (!IsStatus(request.Status))
        {
            return InvalidStatus();
        }

        var content = new MirroredSms(request.Sender, request.Recipient, request.Text, request.Ttl, request.Reference, request.Scenario);
        var message = stores[MirrorStores.Sms].Add(request.Id ?? request.Reference, content, request.Status, request.Detail);
        return Results.Created($"/api/mirror/sms/{message.Id}", new { message.Id });
    }

    private static IResult UpdateStatus(string channel, string id, StatusRequest request, MirrorStores stores)
    {
        if (stores.Find(channel) is not { } store)
        {
            return UnknownChannel(channel, stores);
        }

        if (string.IsNullOrEmpty(request.Status) || !IsStatus(request.Status))
        {
            return InvalidStatus();
        }

        return store.TryUpdateStatus(id, request.Status, request.Detail)
            ? Results.NoContent()
            : Results.NotFound(new { error = $"No {channel} message {id}. It may have been dropped, or the control panel restarted." });
    }

    private static IResult GetMessages(string channel, MirrorStores stores)
    {
        return stores.Find(channel) is { } store ? Results.Ok(store.Messages) : UnknownChannel(channel, stores);
    }

    private static IResult Clear(string channel, MirrorStores stores)
    {
        if (stores.Find(channel) is not { } store)
        {
            return UnknownChannel(channel, stores);
        }

        store.Clear();
        return Results.NoContent();
    }

    private static bool IsStatus(string? status) => status is null || MirrorStatus.All.Contains(status);

    private static IResult InvalidStatus() =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = [$"Status must be one of {string.Join(", ", MirrorStatus.All)}."] });

    private static IResult UnknownChannel(string channel, MirrorStores stores) =>
        Results.NotFound(new { error = $"No channel {channel}. Known: {string.Join(", ", stores.All.Select(s => s.Channel))}." });

    /// <summary>
    /// <c>Id</c> is what status updates use, by default the <c>Reference</c> or a new id. <c>Status</c> is <c>Sent</c> by default, or
    /// e.g. <c>Failed</c> when the mock rejected the message at once.
    /// </summary>
    internal sealed record SmsMirrorRequest(string? Id, string? Sender, string? Recipient, string? Text, int? Ttl, string? Reference, string? Scenario, string? Status, string? Detail);

    internal sealed record StatusRequest(string? Status, string? Detail);
}
