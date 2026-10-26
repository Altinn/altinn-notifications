namespace Altinn.Notifications.Tools.LinkMobilityProxy.Configuration;

/// <summary>
/// Settings for the local LinkMobility proxy.
/// </summary>
public class LinkMobilityProxySettings
{
    /// <summary>
    /// The localhost port the proxy listens on. Point <c>SmsGatewaySettings:Endpoint</c> in the SMS service here.
    /// </summary>
    public int Port { get; set; } = 5093;

    /// <summary>
    /// The LinkMobility gateway to forward to, e.g. <c>https://xml-test.pswin.com</c>.
    /// Empty means stub mode: the proxy answers sends itself, by recipient number, and nothing leaves the machine.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Sends every send to <see cref="FallbackUrl"/> instead of answering it, so an operator answers it in the test harness control panel.
    /// Only in stub mode, and only with a <see cref="FallbackUrl"/>.
    /// </summary>
    public bool Interactive { get; set; }

    /// <summary>
    /// Where requests the stub does not recognise go, as <c>{FallbackUrl}linkmobility/{path}</c>, e.g. the test harness control panel's
    /// <c>http://localhost:5096/intercept/</c>. Empty means a plain 404.
    /// </summary>
    public string FallbackUrl { get; set; } = string.Empty;

    /// <summary>
    /// The SMS service's delivery report endpoint, e.g. <c>http://localhost:5092/notifications/sms/api/v1/reports</c>.
    /// Empty means no delivery reports.
    /// </summary>
    public string DeliveryReportUrl { get; set; } = string.Empty;

    /// <summary>
    /// The basic auth user the SMS service expects on delivery reports, its <c>SmsDeliveryReportSettings:UserSettings:Username</c>.
    /// </summary>
    public string DeliveryReportUsername { get; set; } = "username";

    /// <summary>
    /// The basic auth password the SMS service expects on delivery reports, its <c>SmsDeliveryReportSettings:UserSettings:Password</c>.
    /// </summary>
    public string DeliveryReportPassword { get; set; } = "password";

    /// <summary>
    /// How long after an accepted send the delivery report is sent.
    /// </summary>
    public int DeliveryReportDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Where a copy of each send and its delivery state goes, e.g. the test harness control panel's <c>http://localhost:5096/mirror/</c>,
    /// for its Phone tab. Unmasked, so only point it at localhost. Empty means no copies. A copy that fails is dropped, and never
    /// holds up or fails a send.
    /// </summary>
    public string MirrorUrl { get; set; } = string.Empty;

    /// <summary>
    /// How request and response payloads are logged. <see cref="PayloadLogMode.Plain"/> is for debugging what reaches LinkMobility,
    /// and only starts when the proxy listens on localhost only.
    /// </summary>
    public PayloadLogMode LogPayloads { get; set; } = PayloadLogMode.Masked;

    /// <summary>
    /// A folder that gets a byte-exact copy of each request and response body, with only the password masked, e.g. to attach to
    /// a LinkMobility ticket. Only with <see cref="PayloadLogMode.Plain"/>. Empty means no copies.
    /// </summary>
    public string PayloadFolder { get; set; } = string.Empty;
}

/// <summary>
/// How the proxy logs request and response payloads.
/// </summary>
public enum PayloadLogMode
{
    /// <summary>
    /// The password, recipient numbers and message texts are masked.
    /// </summary>
    Masked,

    /// <summary>
    /// Only the password is masked. Each body is logged as text, as text with invisible characters escaped, and as hex of its bytes.
    /// </summary>
    Plain,
}
