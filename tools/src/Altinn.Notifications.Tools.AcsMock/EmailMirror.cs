using System.Text.Json;

using Altinn.Notifications.Tools.AcsMock.Configuration;
using Altinn.Notifications.Tools.Shared;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Tools.AcsMock;

/// <summary>
/// Sends a copy of each accepted or rejected send and its outcome to <see cref="AcsMockSettings.MirrorUrl"/>, the test harness control
/// panel's Inbox tab.
/// </summary>
internal sealed class EmailMirror(IOptions<AcsMockSettings> options, ILogger<EmailMirror> logger)
    : MirrorSender(options.Value.MirrorUrl, "Email", logger)
{
    /// <summary>
    /// Queues a copy of a send as the mock answered it: accepted as <paramref name="operationId"/>, or rejected with <paramref name="error"/>.
    /// </summary>
    public void Received(string requestBody, string? operationId, EmailScenario scenario, string? error = null)
    {
        if (!IsEnabled || !TryReadEmail(requestBody, out var email))
        {
            return;
        }

        Queue("email", new
        {
            id = operationId,
            email.Sender,
            email.To,
            email.Cc,
            email.Bcc,
            email.Subject,
            email.PlainText,
            email.Html,
            operationId,
            scenario = scenario.ToString(),
            status = error is null ? null : "Failed",
            detail = error,
        });
    }

    /// <summary>
    /// Queues the outcome of an operation: a failed operation, or the status of its delivery report, e.g. <c>Bounced</c>.
    /// </summary>
    public void Completed(string operationId, string outcome)
    {
        string status = outcome == "Delivered" ? "Delivered" : "Failed";
        Queue($"email/{Uri.EscapeDataString(operationId)}/status", new { status, detail = outcome });
    }

    private static bool TryReadEmail(string json, out MirrorEmail email)
    {
        email = new MirrorEmail();
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var content = root.TryGetProperty("content", out var c) ? c : default;
            var recipients = root.TryGetProperty("recipients", out var r) ? r : default;
            email = new MirrorEmail
            {
                Sender = Text(root, "senderAddress"),
                To = Addresses(recipients, "to"),
                Cc = Addresses(recipients, "cc"),
                Bcc = Addresses(recipients, "bcc"),
                Subject = Text(content, "subject"),
                PlainText = Text(content, "plainText"),
                Html = Text(content, "html"),
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }

        static string? Text(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        static string[] Addresses(JsonElement recipients, string kind) =>
            recipients.ValueKind == JsonValueKind.Object && recipients.TryGetProperty(kind, out var list) && list.ValueKind == JsonValueKind.Array
                ? [.. list.EnumerateArray().Select(a => Text(a, "address")).OfType<string>()]
                : [];
    }

    private sealed record MirrorEmail
    {
        public string? Sender { get; init; }

        public string[] To { get; init; } = [];

        public string[] Cc { get; init; } = [];

        public string[] Bcc { get; init; } = [];

        public string? Subject { get; init; }

        public string? PlainText { get; init; }

        public string? Html { get; init; }
    }
}
