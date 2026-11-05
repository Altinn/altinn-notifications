namespace Altinn.Notifications.Tools.TestHarness.Mirror;

/// <summary>
/// An SMS as <c>wiremock-linkmobility</c> received it: LinkMobility's <c>TTL</c> in minutes, the gateway reference the mock answered
/// with (empty when it rejected the message), and the mock's scenario for the recipient, e.g. <c>Undelivered</c>.
/// </summary>
internal sealed record MirroredSms(string? Sender, string Recipient, string? Text, int? Ttl, string? Reference, string? Scenario);

/// <summary>
/// An email as <c>mock-acs</c> received it, with its ACS operation id and the mock's scenario, e.g. <c>Bounced</c>.
/// </summary>
internal sealed record MirroredEmail(
    string? Sender,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    IReadOnlyList<string> Bcc,
    string? Subject,
    string? PlainText,
    string? Html,
    string? OperationId,
    string? Scenario);

/// <summary>
/// <c>/mirror/{channel}</c>, where the mocks send a copy of what they receive and its status, and <c>/api/mirror/{channel}</c>,
/// which the GUI's Phone and Inbox tabs read. Read-only for the GUI: nothing here goes back to the mocks.
/// </summary>
internal static class MirrorEndpoints
{
    public static void MapMirrorEndpoints(this WebApplication app)
    {
        app.MapPost("/mirror/sms", AddSms);
        app.MapPost("/mirror/email", AddEmail);
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

    private static IResult AddEmail(EmailMirrorRequest request, MirrorStores stores)
    {
        string[] to = request.To ?? [];
        string[] cc = request.Cc ?? [];
        string[] bcc = request.Bcc ?? [];
        if (to.Length + cc.Length + bcc.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["to"] = ["An email needs at least one recipient."] });
        }

        if (!IsStatus(request.Status))
        {
            return InvalidStatus();
        }

        var content = new MirroredEmail(request.Sender, to, cc, bcc, request.Subject, request.PlainText, request.Html, request.OperationId, request.Scenario);
        var message = stores[MirrorStores.Email].Add(request.Id ?? request.OperationId, content, request.Status, request.Detail);
        return Results.Created($"/api/mirror/email/{message.Id}", new { message.Id });
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

    /// <summary>
    /// <c>Id</c> is what status updates use, by default the <c>OperationId</c> or a new id.
    /// </summary>
    internal sealed record EmailMirrorRequest(
        string? Id,
        string? Sender,
        string[]? To,
        string[]? Cc,
        string[]? Bcc,
        string? Subject,
        string? PlainText,
        string? Html,
        string? OperationId,
        string? Scenario,
        string? Status,
        string? Detail);

    internal sealed record StatusRequest(string? Status, string? Detail);
}
