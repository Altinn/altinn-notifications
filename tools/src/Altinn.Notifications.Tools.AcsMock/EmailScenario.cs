namespace Altinn.Notifications.Tools.AcsMock;

/// <summary>
/// What happens to a send, chosen by the local part of a recipient address, e.g. <c>bounce@example.com</c>.
/// </summary>
internal enum EmailScenario
{
    /// <summary>Succeeds, then a <c>Delivered</c> report. Any address without a keyword.</summary>
    Delivered,

    /// <summary><c>bounce@</c>: succeeds, then a <c>Bounced</c> report.</summary>
    Bounced,

    /// <summary><c>quarantine@</c>: succeeds, then a <c>Quarantined</c> report.</summary>
    Quarantined,

    /// <summary><c>spam@</c>: succeeds, then a <c>FilteredSpam</c> report.</summary>
    FilteredSpam,

    /// <summary><c>noreport@</c>: succeeds, but no report ever comes.</summary>
    NoReport,

    /// <summary><c>slow@</c>: stays <c>Running</c> for <c>SlowSeconds</c>, then like <see cref="Delivered"/>.</summary>
    Slow,

    /// <summary><c>fail@</c>: the operation ends <c>Failed</c>.</summary>
    Failed,

    /// <summary><c>suppressed@</c>: the operation ends <c>Failed</c> with <c>EmailDroppedAllRecipientsSuppressed</c>.</summary>
    Suppressed,

    /// <summary><c>invalid@</c>: the send is rejected with 400 and an invalid address message.</summary>
    InvalidAddress,

    /// <summary><c>throttle@</c>: the first attempt gets 429 with <c>Retry-After</c>, the next one is like <see cref="Delivered"/>.</summary>
    Throttled,
}

/// <summary>
/// Picks the <see cref="EmailScenario"/> for a send.
/// </summary>
internal static class EmailScenarios
{
    private static readonly Dictionary<string, EmailScenario> _keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bounce"] = EmailScenario.Bounced,
        ["quarantine"] = EmailScenario.Quarantined,
        ["spam"] = EmailScenario.FilteredSpam,
        ["noreport"] = EmailScenario.NoReport,
        ["slow"] = EmailScenario.Slow,
        ["fail"] = EmailScenario.Failed,
        ["suppressed"] = EmailScenario.Suppressed,
        ["invalid"] = EmailScenario.InvalidAddress,
        ["throttle"] = EmailScenario.Throttled,
    };

    /// <summary>
    /// The scenario of the first recipient whose local part, before any <c>+tag</c>, is a keyword.
    /// </summary>
    public static EmailScenario Pick(IEnumerable<string> addresses)
    {
        foreach (string address in addresses)
        {
            string localPart = address.Split('@')[0].Split('+')[0];
            if (_keywords.TryGetValue(localPart, out var scenario))
            {
                return scenario;
            }
        }

        return EmailScenario.Delivered;
    }

    /// <summary>
    /// The delivery report status the API gets for a scenario, or <c>null</c> when no report is sent.
    /// </summary>
    public static string? DeliveryStatus(EmailScenario scenario)
    {
        return scenario switch
        {
            EmailScenario.Delivered or EmailScenario.Slow or EmailScenario.Throttled => "Delivered",
            EmailScenario.Bounced => "Bounced",
            EmailScenario.Quarantined => "Quarantined",
            EmailScenario.FilteredSpam => "FilteredSpam",
            _ => null,
        };
    }
}
