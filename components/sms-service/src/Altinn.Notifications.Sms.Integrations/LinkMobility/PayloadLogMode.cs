namespace Altinn.Notifications.Sms.Integrations.LinkMobility;

/// <summary>
/// How the requests to and responses from the LinkMobility SMS gateway are logged.
/// </summary>
public enum PayloadLogMode
{
    /// <summary>
    /// Payloads are not logged.
    /// </summary>
    Off,

    /// <summary>
    /// Each call is logged with its headers and bodies, with only the password masked. Recipient numbers and message texts are
    /// logged as they are, so this is refused in Production.
    /// </summary>
    Plain,
}
