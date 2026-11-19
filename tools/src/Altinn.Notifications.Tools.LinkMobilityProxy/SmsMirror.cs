using Altinn.Notifications.Tools.LinkMobilityProxy.Configuration;
using Altinn.Notifications.Tools.Shared;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Tools.LinkMobilityProxy;

/// <summary>
/// Sends a copy of each send and its delivery state to <see cref="LinkMobilityProxySettings.MirrorUrl"/>, the test harness control
/// panel's Phone tab.
/// </summary>
internal sealed class SmsMirror(IOptions<LinkMobilityProxySettings> options, ILogger<SmsMirror> logger)
    : MirrorSender(options.Value.MirrorUrl, "SMS", logger)
{
    /// <summary>
    /// Queues a copy of a message as the stub answered it: accepted with a reference, or rejected with an error.
    /// </summary>
    public void Sent(SmsMessage message, SmsResult result, SmsScenario scenario)
    {
        Queue("sms", new
        {
            id = result.Reference,
            sender = message.Sender,
            recipient = message.Recipient,
            text = message.Text,
            ttl = message.Ttl,
            reference = result.Reference,
            scenario = scenario.ToString(),
            status = result.Reference is null ? "Failed" : null,
            detail = result.Error,
        });
    }

    /// <summary>
    /// Queues the state of a delivery report, e.g. <c>DELIVRD</c>, for the message with this gateway reference.
    /// </summary>
    public void Delivered(string reference, string state)
    {
        string status = state switch
        {
            "DELIVRD" => "Delivered",
            "EXPIRED" => "Expired",
            _ => "Failed",
        };

        Queue($"sms/{Uri.EscapeDataString(reference)}/status", new { status, detail = state });
    }
}
