namespace Altinn.Notifications.Tools.LinkMobilityProxy;

/// <summary>
/// What happens to a send in stub mode, chosen by the last two digits of the recipient number, e.g. <c>+4799999901</c>.
/// </summary>
internal enum SmsScenario
{
    /// <summary>Accepted, then a <c>DELIVRD</c> report. <c>00</c> and any number without a scenario.</summary>
    Delivered,

    /// <summary><c>01</c>: accepted, then an <c>UNDELIV</c> report.</summary>
    Undelivered,

    /// <summary><c>02</c>: accepted, then an <c>EXPIRED</c> report.</summary>
    Expired,

    /// <summary><c>03</c>: accepted, but no report ever comes.</summary>
    NoReport,

    /// <summary><c>04</c>: the message is rejected with <c>FAIL</c> and <c>Invalid RCV</c>, so no report.</summary>
    InvalidRecipient,

    /// <summary><c>05</c>: accepted, then a <c>REJECTD</c> report.</summary>
    Rejected,

    /// <summary><c>06</c>: accepted, then a <c>BARRED</c> report.</summary>
    Barred,

    /// <summary><c>07</c>: accepted, then a <c>DELETED</c> report.</summary>
    Deleted,

    /// <summary><c>08</c>: accepted, then a <c>FAILED</c> report.</summary>
    Failed,
}

/// <summary>
/// Picks the <see cref="SmsScenario"/> for a recipient.
/// </summary>
internal static class SmsScenarios
{
    private static readonly Dictionary<string, SmsScenario> _suffixes = new()
    {
        ["01"] = SmsScenario.Undelivered,
        ["02"] = SmsScenario.Expired,
        ["03"] = SmsScenario.NoReport,
        ["04"] = SmsScenario.InvalidRecipient,
        ["05"] = SmsScenario.Rejected,
        ["06"] = SmsScenario.Barred,
        ["07"] = SmsScenario.Deleted,
        ["08"] = SmsScenario.Failed,
    };

    /// <summary>
    /// The scenario of the last two digits of a number. These are also the digits the log keeps.
    /// </summary>
    public static SmsScenario Pick(string recipient)
    {
        string digits = new(recipient.Where(char.IsAsciiDigit).ToArray());
        return digits.Length >= 2 && _suffixes.TryGetValue(digits[^2..], out var scenario) ? scenario : SmsScenario.Delivered;
    }

    /// <summary>
    /// The <c>STATE</c> of the delivery report for a scenario, or <c>null</c> when no report is sent.
    /// </summary>
    public static string? DeliveryState(SmsScenario scenario)
    {
        return scenario switch
        {
            SmsScenario.Delivered => "DELIVRD",
            SmsScenario.Undelivered => "UNDELIV",
            SmsScenario.Expired => "EXPIRED",
            SmsScenario.Rejected => "REJECTD",
            SmsScenario.Barred => "BARRED",
            SmsScenario.Deleted => "DELETED",
            SmsScenario.Failed => "FAILED",
            _ => null,
        };
    }
}
